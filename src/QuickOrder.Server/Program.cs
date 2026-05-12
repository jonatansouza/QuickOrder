using QuickOrder.Infrastructure.MessageCrackers;
using QuickOrder.Infrastructure.Repositories;
using QuickOrder.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddSingleton<ServerFixApplication>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
