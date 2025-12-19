using GreenbotTwo.Commands;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Configuration.Models.Endpoints;
using GreenbotTwo.Interactions;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Models;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Credentials;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Rest;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

namespace GreenbotTwo;

public static class StartupExtensions
{
    
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
        services.AddTransient<IApplicationService<BuilderApplicationForm, BuilderApplication>, BuilderApplicationService>();
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
            .AddDiscordRest()
            .AddDiscordGateway()
            .AddApplicationCommands()
            .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
            .AddComponentInteractions<ModalInteraction, ModalInteractionContext>();
        
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
        
        return app;
    }
    
}