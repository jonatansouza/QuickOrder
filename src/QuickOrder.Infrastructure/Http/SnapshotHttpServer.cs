namespace QuickOrder.Infrastructure.Http;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickOrder.Core.Application.UseCases;

public class SnapshotHttpServer : IHostedService
{
    private readonly ILogger<SnapshotHttpServer> _logger;
    private readonly GetBookSnapshotHandler _snapshot;
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public SnapshotHttpServer(ILogger<SnapshotHttpServer> logger, GetBookSnapshotHandler snapshot)
    {
        _logger = logger;
        _snapshot = snapshot;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var port = Environment.GetEnvironmentVariable("SERVER_SNAPSHOT_PORT") ?? "7001";
        var bind = Environment.GetEnvironmentVariable("SNAPSHOT_BIND") ?? "+";
        _listener.Prefixes.Add($"http://{bind}:{port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => AcceptLoop(_cts.Token));
        _logger.LogInformation("[Server] Snapshot HTTP listening on {Bind}:{Port}", bind, port);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_listener.IsListening) _listener.Stop();
        return _loopTask ?? Task.CompletedTask;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.HttpMethod == "GET" && ctx.Request.Url?.AbsolutePath == "/book")
            {
                var snapshot = BuildSnapshot();
                var json = JsonSerializer.SerializeToUtf8Bytes(snapshot);
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
                await ctx.Response.OutputStream.WriteAsync(json);
            }
            else
            {
                ctx.Response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Server] Snapshot HTTP error");
            ctx.Response.StatusCode = 500;
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private object BuildSnapshot()
    {
        return _snapshot.Handle()
            .Select(g => new
            {
                symbol = g.Symbol.ToString(),
                side = g.Side.ToString().ToUpperInvariant(),
                orders = g.Orders.Select(o => new
                {
                    price = o.Price,
                    quantity = o.Quantity
                }).ToArray()
            })
            .ToArray();
    }
}
