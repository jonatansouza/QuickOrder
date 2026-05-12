namespace QuickOrder.Infrastructure.MessageCrackers;

using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using QuickOrder.Infrastructure.Ledger;
using System.Collections.Concurrent;

public class ClientFixApplication : MessageCracker, IApplication
{
    private readonly ILogger<ClientFixApplication> _logger;
    private readonly OrderLedger _ledger;
    private ThreadedSocketAcceptor? _acceptor;
    private SocketInitiator? _initiator;
    private SessionID? _serverSession;
    private readonly ConcurrentDictionary<string, SessionID> _pendingOrders = new();

    public ClientFixApplication(ILogger<ClientFixApplication> logger, OrderLedger ledger)
    {
        _logger = logger;
        _ledger = ledger;
    }

    public void Start()
    {
        var serverHost = Environment.GetEnvironmentVariable("SERVER_HOST") ?? "localhost";

        var acceptorSettings = new SessionSettings(new StringReader(@"
[DEFAULT]
ConnectionType=acceptor
SocketAcceptPort=5000
StartTime=00:00:00
EndTime=00:00:00
UseDataDictionary=N
ResetOnLogon=Y
HeartBtInt=30

[SESSION]
BeginString=FIX.4.2
SenderCompID=CLIENT
TargetCompID=SIMULATOR
"));
        _acceptor = new ThreadedSocketAcceptor(this, new MemoryStoreFactory(), acceptorSettings, new NullLogFactory());
        _acceptor.Start();
        _logger.LogInformation("[Client] FIX Acceptor started on port 5000");

        var initiatorSettings = new SessionSettings(new StringReader($@"
[DEFAULT]
ConnectionType=initiator
SocketConnectHost={serverHost}
SocketConnectPort=5001
StartTime=00:00:00
EndTime=00:00:00
UseDataDictionary=N
ResetOnLogon=Y
HeartBtInt=30
ReconnectInterval=5

[SESSION]
BeginString=FIX.4.2
SenderCompID=CLIENT
TargetCompID=SERVER
"));
        _initiator = new SocketInitiator(this, new MemoryStoreFactory(), initiatorSettings, new NullLogFactory());
        _initiator.Start();
        _logger.LogInformation("[Client] FIX Initiator started, connecting to {Host}:5001", serverHost);
    }

    public void Stop()
    {
        _acceptor?.Stop();
        _initiator?.Stop();
    }

    public void OnCreate(SessionID sessionID) { }

    public void OnLogon(SessionID sessionID)
    {
        if (sessionID.TargetCompID == "SERVER")
        {
            _serverSession = sessionID;
            _logger.LogInformation("[Client] Connected to Server");
        }
        else
            _logger.LogInformation("[Client] External user connected: {CompID}", sessionID.TargetCompID);
    }

    public void OnLogout(SessionID sessionID)
    {
        if (sessionID.TargetCompID == "SERVER")
        {
            _serverSession = null;
            _logger.LogInformation("[Client] Disconnected from Server");
        }
        else
            _logger.LogInformation("[Client] External user disconnected: {CompID}", sessionID.TargetCompID);
    }

    public void ToAdmin(Message message, SessionID sessionID) { }
    public void ToApp(Message message, SessionID sessionID) { }
    public void FromAdmin(Message message, SessionID sessionID) { }

    public void FromApp(Message message, SessionID sessionID)
    {
        _logger.LogInformation("[Client] Received MsgType={MsgType} from {CompID}",
            message.Header.GetString(Tags.MsgType), sessionID.TargetCompID);
        Crack(message, sessionID);
    }

    // From Simulator → forward to Server
    public void OnMessage(QuickFix.FIX42.NewOrderSingle order, SessionID sessionID)
    {
        var clOrdId = order.ClOrdID.Value;

        if (_serverSession == null)
        {
            Session.SendToTarget(
                BuildOrderRejection(clOrdId, order.Symbol.Value, order.Side.Value, "servidor indisponivel"),
                sessionID);
            _logger.LogWarning("[Client] Server unavailable, rejected order {ClOrdId}", clOrdId);
            return;
        }

        _pendingOrders[clOrdId] = sessionID;
        Session.SendToTarget(order, _serverSession);
        _logger.LogInformation("[Client] Forwarded NewOrder {ClOrdId} to Server", clOrdId);
    }

    // From Simulator → forward cancel to Server
    public void OnMessage(QuickFix.FIX42.OrderCancelRequest cancel, SessionID sessionID)
    {
        var clOrdId = cancel.ClOrdID.Value;

        if (_serverSession == null)
        {
            Session.SendToTarget(
                BuildCancelRejection(clOrdId, cancel.OrigClOrdID.Value, "servidor indisponivel"),
                sessionID);
            _logger.LogWarning("[Client] Server unavailable, rejected cancel {ClOrdId}", clOrdId);
            return;
        }

        _pendingOrders[clOrdId] = sessionID;
        Session.SendToTarget(cancel, _serverSession);
        _logger.LogInformation("[Client] Forwarded CancelRequest {ClOrdId} to Server", clOrdId);
    }

    // From Server → forward back to Simulator
    public void OnMessage(QuickFix.FIX42.ExecutionReport report, SessionID sessionID)
    {
        var clOrdId = report.IsSetClOrdID() ? report.ClOrdID.Value : string.Empty;
        var status  = report.OrdStatus.Value.ToString();
        var text    = report.IsSetText() ? report.Text.Value : null;

        _ledger.Append(new LedgerEntry(DateTime.UtcNow, "ExecutionReport", clOrdId, status, text));

        if (_pendingOrders.TryRemove(clOrdId, out var externalSession))
        {
            Session.SendToTarget(report, externalSession);
            _logger.LogInformation("[Client] Forwarded ExecutionReport {ClOrdId} back to external", clOrdId);
        }
    }

    // From Server → forward cancel rejection back to Simulator
    public void OnMessage(QuickFix.FIX42.OrderCancelReject reject, SessionID sessionID)
    {
        var clOrdId = reject.IsSetClOrdID() ? reject.ClOrdID.Value : string.Empty;
        var text    = reject.IsSetText() ? reject.Text.Value : null;

        _ledger.Append(new LedgerEntry(DateTime.UtcNow, "OrderCancelReject", clOrdId, "REJECTED", text));

        if (_pendingOrders.TryRemove(clOrdId, out var externalSession))
        {
            Session.SendToTarget(reject, externalSession);
            _logger.LogInformation("[Client] Forwarded CancelReject {ClOrdId} back to external", clOrdId);
        }
    }

    private static QuickFix.FIX42.ExecutionReport BuildOrderRejection(string clOrdId, string symbol, char side, string text)
    {
        var report = new QuickFix.FIX42.ExecutionReport(
            new OrderID(clOrdId),
            new ExecID(Guid.NewGuid().ToString("N")[..8]),
            new ExecTransType(ExecTransType.NEW),
            new ExecType(ExecType.REJECTED),
            new OrdStatus(OrdStatus.REJECTED),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(0),
            new CumQty(0),
            new AvgPx(0)
        );
        report.Set(new ClOrdID(clOrdId));
        report.Set(new Text(text));
        return report;
    }

    private static QuickFix.FIX42.OrderCancelReject BuildCancelRejection(string clOrdId, string origClOrdId, string text)
    {
        var reject = new QuickFix.FIX42.OrderCancelReject(
            new OrderID(origClOrdId),
            new ClOrdID(clOrdId),
            new OrigClOrdID(origClOrdId),
            new OrdStatus(OrdStatus.REJECTED),
            new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST)
        );
        reject.Set(new CxlRejReason(CxlRejReason.OTHER));
        reject.Set(new Text(text));
        return reject;
    }
}
