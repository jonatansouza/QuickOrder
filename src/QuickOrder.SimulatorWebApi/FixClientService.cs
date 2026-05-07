using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using System.Collections.Concurrent;

namespace QuickOrder.SimulatorWebApi;

public record OrderRequest(string ClOrdId, string Symbol, string Side, int Qty, decimal Price);
public record OrderResult(string ClOrdId, string OrdStatus, string Message);

public class FixClientService : MessageCracker, IApplication, IHostedService
{
    private readonly ILogger<FixClientService> _logger;
    private SocketInitiator? _initiator;
    private SessionID? _sessionId;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<OrderResult>> _pending = new();

    public FixClientService(ILogger<FixClientService> logger) => _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var clientHost = Environment.GetEnvironmentVariable("CLIENT_HOST") ?? "localhost";
        var settings = new SessionSettings(new StringReader($@"
[DEFAULT]
ConnectionType=initiator
SocketConnectHost={clientHost}
SocketConnectPort=5000
StartTime=00:00:00
EndTime=00:00:00
UseDataDictionary=N
ResetOnLogon=Y
HeartBtInt=30
ReconnectInterval=5

[SESSION]
BeginString=FIX.4.2
SenderCompID=SIMULATOR
TargetCompID=CLIENT
"));
        _initiator = new SocketInitiator(this, new MemoryStoreFactory(), settings, new NullLogFactory());
        _initiator.Start();
        _logger.LogInformation("[Simulator] Connecting to Client at {Host}:5000", clientHost);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _initiator?.Stop();
        return Task.CompletedTask;
    }

    public async Task<OrderResult> SendOrderAsync(OrderRequest req, CancellationToken ct = default)
    {
        if (_sessionId == null)
            throw new InvalidOperationException("Not connected to Client");

        var sideChar = req.Side.ToUpper() == "BUY" ? Side.BUY : Side.SELL;

        var tcs = new TaskCompletionSource<OrderResult>();
        _pending[req.ClOrdId] = tcs;

        var order = new QuickFix.FIX42.NewOrderSingle(
            new ClOrdID(req.ClOrdId),
            new HandlInst('1'),
            new Symbol(req.Symbol),
            new Side(sideChar),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT)
        );
        order.Set(new OrderQty(req.Qty));
        order.Set(new Price(req.Price));

        Session.SendToTarget(order, _sessionId);
        _logger.LogInformation("[Simulator] Sent NewOrderSingle: {ClOrdId} {Symbol} {Side} {Qty} @ {Price}",
            req.ClOrdId, req.Symbol, req.Side, req.Qty, req.Price);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            return await tcs.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(req.ClOrdId, out _);
            throw new TimeoutException($"No response for {req.ClOrdId} within 5s");
        }
    }

    public void OnCreate(SessionID sessionID) { }
    public void OnLogon(SessionID sessionID) { _sessionId = sessionID; _logger.LogInformation("[Simulator] Connected to Client"); }
    public void OnLogout(SessionID sessionID) { _sessionId = null; _logger.LogInformation("[Simulator] Disconnected from Client"); }
    public void ToAdmin(Message message, SessionID sessionID) { }
    public void ToApp(Message message, SessionID sessionID) { }
    public void FromAdmin(Message message, SessionID sessionID) { }
    public void FromApp(Message message, SessionID sessionID) => Crack(message, sessionID);

    public void OnMessage(QuickFix.FIX42.ExecutionReport report, SessionID sessionID)
    {
        var clOrdId = report.IsSetClOrdID() ? report.ClOrdID.Value : string.Empty;
        var status = report.OrdStatus.Value.ToString();
        _logger.LogInformation("[Simulator] ExecutionReport: ClOrdId={ClOrdId} OrdStatus={Status}", clOrdId, status);

        if (_pending.TryRemove(clOrdId, out var tcs))
            tcs.SetResult(new OrderResult(clOrdId, status, $"OrdStatus={status}"));
    }
}
