using QuickOrder.Client;
using QuickOrder.Infrastructure.MessageCrackers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ClientFixApplication>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
