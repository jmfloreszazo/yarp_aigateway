using Yarp.AiGateway.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAiGatewayFromJson("aigateway.json");

var app = builder.Build();

app.UseAiGateway();

app.Run();
