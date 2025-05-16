using RoyalCode.SmartCommands.Demo;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();

var app = builder.Build();

app.ConfigurePipeline();

app.Run();
