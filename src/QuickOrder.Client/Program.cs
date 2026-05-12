using QuickOrder.Client;
using QuickOrder.Infrastructure.Http;
using QuickOrder.Infrastructure.Ledger;
using QuickOrder.Infrastructure.MessageCrackers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<OrderLedger>();
builder.Services.AddSingleton<ClientFixAdapter>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<SnapshotProxyHttpServer>();

var host = builder.Build();
host.Run();
