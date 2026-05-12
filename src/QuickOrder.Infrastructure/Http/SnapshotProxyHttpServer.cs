namespace QuickOrder.Infrastructure.Http;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickOrder.Infrastructure.Ledger;

public class SnapshotProxyHttpServer : IHostedService
{
    private readonly ILogger<SnapshotProxyHttpServer> _logger;
    private readonly OrderLedger _ledger;
    private readonly HttpListener _listener = new();
    private readonly HttpClient _http = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private string _serverUrl = string.Empty;

    public SnapshotProxyHttpServer(ILogger<SnapshotProxyHttpServer> logger, OrderLedger ledger)
    {
        _logger = logger;
        _ledger = ledger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var port = Environment.GetEnvironmentVariable("CLIENT_SNAPSHOT_PORT") ?? "7000";
        var bind = Environment.GetEnvironmentVariable("SNAPSHOT_BIND") ?? "+";
        var serverHost = Environment.GetEnvironmentVariable("SERVER_HOST") ?? "localhost";
        var serverPort = Environment.GetEnvironmentVariable("SERVER_SNAPSHOT_PORT") ?? "7001";
        _serverUrl = $"http://{serverHost}:{serverPort}/book";

        _listener.Prefixes.Add($"http://{bind}:{port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => AcceptLoop(_cts.Token));
        _logger.LogInformation("[Client] Snapshot HTTP on {Bind}:{Port} -> {ServerUrl}", bind, port, _serverUrl);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_listener.IsListening) _listener.Stop();
        _http.Dispose();
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
            var path = ctx.Request.Url?.AbsolutePath;
            if (ctx.Request.HttpMethod == "GET" && path == "/book")
            {
                using var resp = await _http.GetAsync(_serverUrl);
                ctx.Response.StatusCode = (int)resp.StatusCode;
                ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json";
                await resp.Content.CopyToAsync(ctx.Response.OutputStream);
            }
            else if (ctx.Request.HttpMethod == "GET" && path == "/ledger")
            {
                var json = JsonSerializer.SerializeToUtf8Bytes(_ledger.Snapshot());
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
            _logger.LogError(ex, "[Client] Snapshot proxy error");
            ctx.Response.StatusCode = 502;
        }
        finally
        {
            ctx.Response.Close();
        }
    }
}
