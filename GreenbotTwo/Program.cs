using GreenbotTwo;
using GreenbotTwo.Configuration.Models.Endpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.LoadConfigurationFiles(builder.Environment);
builder.ConfigureSerilog();


try
{
    builder.Services
        .LoadConfigurationOptions(builder.Configuration)
        .ConfigureServices()
        .ConfigureClientServices(builder.Configuration.GetSection("ApiEndpoints").Get<ApiEndpointSettings>()!)
        .ConfigureDiscordServices();

    var app = builder.Build()
        .ConfigureCommands()
        .ConfigureInteractions();
    
    await app.RunAsync();   
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}