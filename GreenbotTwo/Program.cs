using GreenbotTwo;
using GreenbotTwo.Configuration.Models.Endpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.LoadConfigurationFiles(builder.Environment);

builder.Services
    .LoadConfigurationOptions(builder.Configuration)
    .ConfigureServices()
    .ConfigureClientServices(builder.Configuration.GetSection("ApiEndpoints").Get<ApiEndpointSettings>()!)
    .ConfigureDiscordServices();

var app = builder.Build()
    .ConfigureCommands()
    .ConfigureInteractions();

await app.RunAsync();