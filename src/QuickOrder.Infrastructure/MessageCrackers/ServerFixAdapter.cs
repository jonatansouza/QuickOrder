namespace QuickOrder.Infrastructure.MessageCrackers;

using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using QuickOrder.Core.Application.Commands;
using QuickOrder.Core.Application.UseCases;

public class ServerFixAdapter : MessageCracker, IApplication
{
    private readonly ILogger<ServerFixAdapter> _logger;
    private readonly PlaceOrderHandler _placeOrder;
    private readonly CancelOrderHandler _cancelOrder;
    private ThreadedSocketAcceptor? _acceptor;

    public ServerFixAdapter(
        ILogger<ServerFixAdapter> logger,
        PlaceOrderHandler placeOrder,
        CancelOrderHandler cancelOrder)
    {
        _logger = logger;
        _placeOrder = placeOrder;
        _cancelOrder = cancelOrder;
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
        var clOrdId   = newOrder.ClOrdID.Value;
        var fixSymbol = newOrder.Symbol.Value;
        var fixSide   = newOrder.Side.Value;
        var qty       = newOrder.IsSetOrderQty() ? (int)newOrder.OrderQty.Value : 0;
        var price     = newOrder.IsSetPrice()    ? newOrder.Price.Value          : 0m;

        if (!FixMapping.TryParseSymbol(fixSymbol, out var symbol))
        {
            Session.SendToTarget(BuildReject(clOrdId, fixSymbol, fixSide, "Simbolo invalido"), sessionID);
            _logger.LogInformation("[Server] Rejected {ClOrdId}: Simbolo invalido", clOrdId);
            return;
        }
        if (!FixMapping.TryParseSide(fixSide, out var side))
        {
            Session.SendToTarget(BuildReject(clOrdId, fixSymbol, fixSide, "Lado invalido"), sessionID);
            _logger.LogInformation("[Server] Rejected {ClOrdId}: Lado invalido", clOrdId);
            return;
        }

        var result = _placeOrder.Handle(new NewOrderCommand(clOrdId, symbol, side, qty, price));

        if (result.Accepted)
        {
            Session.SendToTarget(BuildAccept(clOrdId, fixSymbol, fixSide, qty), sessionID);
            _logger.LogInformation("[Server] Accepted {ClOrdId}", clOrdId);
        }
        else
        {
            Session.SendToTarget(BuildReject(clOrdId, fixSymbol, fixSide, result.RejectionReason!), sessionID);
            _logger.LogInformation("[Server] Rejected {ClOrdId}: {Reason}", clOrdId, result.RejectionReason);
        }
    }

    public void OnMessage(QuickFix.FIX42.OrderCancelRequest cancel, SessionID sessionID)
    {
        var clOrdId     = cancel.ClOrdID.Value;
        var origClOrdId = cancel.OrigClOrdID.Value;
        var fixSymbol   = cancel.Symbol.Value;
        var fixSide     = cancel.Side.Value;

        var result = _cancelOrder.Handle(new CancelOrderCommand(clOrdId, origClOrdId));

        if (result.Accepted)
        {
            Session.SendToTarget(BuildCancelAccept(clOrdId, origClOrdId, fixSymbol, fixSide), sessionID);
            _logger.LogInformation("[Server] Cancelled {OrigClOrdId} via {ClOrdId}", origClOrdId, clOrdId);
        }
        else
        {
            Session.SendToTarget(BuildCancelReject(clOrdId, origClOrdId, result.RejectionReason!), sessionID);
            _logger.LogInformation("[Server] CancelReject {OrigClOrdId}: {Reason}", origClOrdId, result.RejectionReason);
        }
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

    private static QuickFix.FIX42.ExecutionReport BuildCancelAccept(string clOrdId, string origClOrdId, string symbol, char side)
    {
        var report = new QuickFix.FIX42.ExecutionReport(
            new OrderID(origClOrdId),
            new ExecID(Guid.NewGuid().ToString("N")[..8]),
            new ExecTransType(ExecTransType.NEW),
            new ExecType(ExecType.CANCELED),
            new OrdStatus(OrdStatus.CANCELED),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(0),
            new CumQty(0),
            new AvgPx(0)
        );
        report.Set(new ClOrdID(clOrdId));
        report.Set(new OrigClOrdID(origClOrdId));
        return report;
    }

    private static QuickFix.FIX42.OrderCancelReject BuildCancelReject(string clOrdId, string origClOrdId, string text)
    {
        var reject = new QuickFix.FIX42.OrderCancelReject(
            new OrderID(origClOrdId),
            new ClOrdID(clOrdId),
            new OrigClOrdID(origClOrdId),
            new OrdStatus(OrdStatus.REJECTED),
            new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST)
        );
        reject.Set(new CxlRejReason(CxlRejReason.UNKNOWN_ORDER));
        reject.Set(new Text(text));
        return reject;
    }
}
