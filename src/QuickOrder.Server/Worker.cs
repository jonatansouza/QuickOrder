namespace QuickOrder.Server;

using QuickOrder.Infrastructure.MessageCrackers;

public class Worker : BackgroundService
{
    private readonly ServerFixAdapter _fixApp;

    public Worker(ServerFixAdapter fixApp) => _fixApp = fixApp;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _fixApp.Start();
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (TaskCanceledException) { }
        _fixApp.Stop();
    }
}
