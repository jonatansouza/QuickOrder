using FluentValidation;
using QuickOrder.SimulatorWebApi;
using QuickOrder.SimulatorWebApi.Controllers;
using QuickOrder.SimulatorWebApi.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IValidator<NewOrderPayload>, NewOrderPayloadValidator>();
builder.Services.AddScoped<IValidator<CancelOrderPayload>, CancelOrderPayloadValidator>();

builder.Services.AddSingleton<FixClientService>();
builder.Services.AddHostedService(p => p.GetRequiredService<FixClientService>());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
