namespace QuickOrder.Infrastructure.MessageCrackers;

using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using QuickOrder.Core.Domain;
using QuickOrder.Infrastructure.Repositories;

public class ServerFixApplication : MessageCracker, IApplication
{
    private readonly ILogger<ServerFixApplication> _logger;
    private readonly OrderRepository _orders;
    private ThreadedSocketAcceptor? _acceptor;

    public ServerFixApplication(ILogger<ServerFixApplication> logger, OrderRepository orders)
    {
        _logger = logger;
        _orders = orders;
    }

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

    public void OnMessage(QuickFix.FIX42.NewOrderSingle newOrder, SessionID sessionID)
    {
        var clOrdId = newOrder.ClOrdID.Value;
        var symbol  = newOrder.Symbol.Value;
        var side    = newOrder.Side.Value;
        var qty     = newOrder.IsSetOrderQty() ? (int)newOrder.OrderQty.Value : 0;
        var price   = newOrder.IsSetPrice()    ? newOrder.Price.Value          : 0m;

        if (!Order.TryCreate(clOrdId, symbol, side, qty, price, out var order, out var reason))
        {
            Session.SendToTarget(BuildReject(clOrdId, symbol, side, reason!), sessionID);
            _logger.LogInformation("[Server] Rejected {ClOrdId}: {Reason}", clOrdId, reason);
            return;
        }

        if (!_orders.TryAdd(order!))
        {
            Session.SendToTarget(BuildReject(clOrdId, symbol, side, "ClOrdId duplicado"), sessionID);
            _logger.LogInformation("[Server] Rejected {ClOrdId}: duplicate", clOrdId);
            return;
        }

        Session.SendToTarget(BuildAccept(clOrdId, symbol, side, qty), sessionID);
        _logger.LogInformation("[Server] Accepted {ClOrdId}", clOrdId);
    }

    private static QuickFix.FIX42.ExecutionReport BuildAccept(string clOrdId, string symbol, char side, int qty)
    {
        var report = new QuickFix.FIX42.ExecutionReport(
            new OrderID(clOrdId),
            new ExecID(Guid.NewGuid().ToString("N")[..8]),
            new ExecTransType(ExecTransType.NEW),
            new ExecType(ExecType.NEW),
            new OrdStatus(OrdStatus.NEW),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(qty),
            new CumQty(0),
            new AvgPx(0)
        );
        report.Set(new ClOrdID(clOrdId));
        return report;
    }

    private static QuickFix.FIX42.ExecutionReport BuildReject(string clOrdId, string symbol, char side, string text)
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
}
