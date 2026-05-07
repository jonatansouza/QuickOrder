namespace QuickOrder.Infrastructure.MessageCrackers;

using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

public class ServerFixApplication : MessageCracker, IApplication
{
    private readonly ILogger<ServerFixApplication> _logger;
    private ThreadedSocketAcceptor? _acceptor;

    public ServerFixApplication(ILogger<ServerFixApplication> logger) => _logger = logger;

    public void Start()
    {
        var settings = new SessionSettings(new StringReader(@"
[DEFAULT]
ConnectionType=acceptor
SocketAcceptPort=5001
StartTime=00:00:00
EndTime=00:00:00
UseDataDictionary=N
ResetOnLogon=Y
HeartBtInt=30

[SESSION]
BeginString=FIX.4.2
SenderCompID=SERVER
TargetCompID=CLIENT
"));
        _acceptor = new ThreadedSocketAcceptor(this, new MemoryStoreFactory(), settings, new NullLogFactory());
        _acceptor.Start();
        _logger.LogInformation("[Server] FIX Acceptor started on port 5001");
    }

    public void Stop() => _acceptor?.Stop();

    public void OnCreate(SessionID sessionID) { }
    public void OnLogon(SessionID sessionID) => _logger.LogInformation("[Server] Client logged on: {Session}", sessionID);
    public void OnLogout(SessionID sessionID) => _logger.LogInformation("[Server] Client logged off: {Session}", sessionID);
    public void ToAdmin(Message message, SessionID sessionID) { }
    public void ToApp(Message message, SessionID sessionID) { }
    public void FromAdmin(Message message, SessionID sessionID) { }

    public void FromApp(Message message, SessionID sessionID)
    {
        _logger.LogInformation("[Server] Received MsgType={MsgType}", message.Header.GetString(Tags.MsgType));
        Crack(message, sessionID);
    }

    public void OnMessage(QuickFix.FIX42.NewOrderSingle order, SessionID sessionID)
    {
        var clOrdId = order.ClOrdID.Value;
        var symbol = order.Symbol.Value;
        var qty = order.IsSetOrderQty() ? order.OrderQty.Value : 0m;

        _logger.LogInformation("[Server] NewOrder: ClOrdId={ClOrdId} Symbol={Symbol} Qty={Qty}", clOrdId, symbol, qty);

        var report = new QuickFix.FIX42.ExecutionReport(
            new OrderID(clOrdId),
            new ExecID(Guid.NewGuid().ToString("N")[..8]),
            new ExecTransType(ExecTransType.NEW),
            new ExecType(ExecType.NEW),
            new OrdStatus(OrdStatus.NEW),
            new Symbol(symbol),
            new Side(order.Side.Value),
            new LeavesQty(qty),
            new CumQty(0),
            new AvgPx(0)
        );
        report.Set(new ClOrdID(clOrdId));

        Session.SendToTarget(report, sessionID);
        _logger.LogInformation("[Server] Sent ExecutionReport: accepted ClOrdId={ClOrdId}", clOrdId);
    }
}
