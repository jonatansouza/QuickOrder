using QuickOrder.Core.Application.UseCases;
using QuickOrder.Core.Domain.Abstractions;
using QuickOrder.Infrastructure.Http;
using QuickOrder.Infrastructure.MessageCrackers;
using QuickOrder.Infrastructure.Repositories;
using QuickOrder.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddSingleton<IOrderBook>(sp => sp.GetRequiredService<OrderRepository>());
builder.Services.AddSingleton<PlaceOrderHandler>();
builder.Services.AddSingleton<CancelOrderHandler>();
builder.Services.AddSingleton<GetBookSnapshotHandler>();
builder.Services.AddSingleton<ServerFixAdapter>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<SnapshotHttpServer>();

var host = builder.Build();
host.Run();
