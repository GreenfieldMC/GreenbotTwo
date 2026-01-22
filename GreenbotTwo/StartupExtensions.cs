using GreenbotTwo.Commands;
using GreenbotTwo.Commands.ApplicationCommands;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Configuration.Models.Endpoints;
using GreenbotTwo.Interactions;
using GreenbotTwo.Interactions.AccountLink;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Credentials;
using GreenbotTwo.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Rest;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;
using Serilog;
using Serilog.Exceptions;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;

namespace GreenbotTwo;

public static class StartupExtensions
{
    
    internal static void ConfigureSerilog(this HostApplicationBuilder hostBuilder)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .ReadFrom.Configuration(hostBuilder.Configuration)
            .CreateLogger();
        
        hostBuilder.Logging.AddSerilog(logger: Log.Logger);
    }
    
    /// <summary>
    /// Loads configuration files into the configuration builder.
    /// </summary>
    /// <param name="configBuilder"></param>
    /// <param name="env"></param>
    internal static void LoadConfigurationFiles(this IConfigurationBuilder configBuilder, IHostEnvironment env)
    {
        configBuilder.SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    }

    /// <summary>
    /// Loads configuration options into the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    internal static IServiceCollection LoadConfigurationOptions(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ApplicationSettings>(config);
        services.Configure<ApiEndpointSettings>(config.GetSection("ApiEndpoints"));
        services.Configure<BetapackCommandSettings>(config.GetSection("CommandSettings:BetapackCommand"));
        services.Configure<InstallCommandSettings>(config.GetSection("CommandSettings:InstallCommand"));
        services.AddOptionsWithValidateOnStart<BuilderApplicationSettings>()
            .BindConfiguration("BuilderApplicationSettings");
        return services;
    }

    /// <summary>
    /// Configures application services into the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    internal static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddTransient<OAuthClientCredentialsHandler>();
        services.AddTransient<IApplicationService, ApplicationService>();
        services.AddTransient<IAccountLinkService, AccountLinkService>();
        return services;
    }
    
    /// <summary>
    /// Configures API client services into the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="apiEndpointSettings"></param>
    /// <returns></returns>
    internal static IServiceCollection ConfigureClientServices(this IServiceCollection services, ApiEndpointSettings apiEndpointSettings)
    {
        services.AddHttpClient<IGreenfieldApiService, GreenfieldApiService>(client =>
            {
                client.BaseAddress = new Uri(apiEndpointSettings.GreenfieldCoreApi.BaseAddress);
            })
            .AddHttpMessageHandler<OAuthClientCredentialsHandler>();

        services.AddHttpClient<IMojangService, MojangService>(client =>
        {
            client.BaseAddress = new Uri(apiEndpointSettings.MojangApi.BaseAddress);
        });
        services.AddHttpClient<IAuthenticationHubService, AuthenticationHubService>(client =>
        {
            client.BaseAddress = new Uri(apiEndpointSettings.AuthenticationHubApi.BaseAddress);
        });
        return services;
    }

    /// <summary>
    /// Configures Discord services into the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    internal static IServiceCollection ConfigureDiscordServices(this IServiceCollection services)
    {
        services
            .AddSerilog(Log.Logger)
            .AddDiscordRest()
            .AddDiscordGateway()
            .AddApplicationCommands()
            .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
            .AddComponentInteractions<ModalInteraction, ModalInteractionContext>()
            .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>();
        
        return services;
    }

    /// <summary>
    /// Configures application commands into the host.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    internal static IHost ConfigureCommands(this IHost app)
    {
        app.AddApplicationCommandModule<InstallCommand>();
        app.AddApplicationCommandModule<CodesCommand>();
        app.AddApplicationCommandModule<BetapackCommand>();
        app.AddApplicationCommandModule<ApplyCommand>();
        app.AddApplicationCommandModule<AccountsCommand>();
        app.AddApplicationCommandModule<AppStatusCommand>();
        app.AddApplicationCommandModule<ViewAppCommand>();
        return app;
    }
    
    /// <summary>
    /// Configures component interactions into the host.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    internal static IHost ConfigureInteractions(this IHost app)
    {
        app.AddComponentInteractionModule<ButtonInteractionContext, ApplyInteractions.ApplyMessageButtonInteractions>();
        app.AddComponentInteractionModule<ButtonInteractionContext, ReviewInteractions.ReviewButtonInteractions>();
        app.AddComponentInteractionModule<ModalInteractionContext, ApplyInteractions.ApplyModalInteractions>();
        app.AddComponentInteractionModule<ModalInteractionContext, ReviewInteractions.ReviewModalInteractions>();
        app.AddComponentInteractionModule<StringMenuInteractionContext, ApplyInteractions.ApplyUserSelectionInteractions>();
        app.AddComponentInteractionModule<ButtonInteractionContext, AccountLinkInteractions.AccountLinkButtonInteractions>();
        app.AddComponentInteractionModule<ModalInteractionContext, AccountLinkInteractions.AuthCodeModalInteractions>();
        app.AddComponentInteractionModule<StringMenuInteractionContext, AccountLinkInteractions.AccountLinkSelectUserInteractions>();
        
        return app;
    }
    
}