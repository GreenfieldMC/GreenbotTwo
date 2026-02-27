using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands.TypeReaders;

namespace GreenbotTwo.NetCordSupport.TypeReaders;

public class ApplicationTypeReader<TContext> : SlashCommandTypeReader<TContext> where TContext : IApplicationCommandContext
{
    public override async ValueTask<SlashCommandTypeReaderResult> ReadAsync(string value, TContext context, SlashCommandParameter<TContext> parameter, ApplicationCommandServiceConfiguration<TContext> configuration, IServiceProvider? serviceProvider)
    {
        if (!long.TryParse(value, out var applicationId))
            return SlashCommandTypeReaderResult.Fail("Invalid application ID format. Please provide a valid numeric ID.");

        var gfApiService = serviceProvider?.GetRequiredService<IGreenfieldApiService>() ?? throw new InvalidOperationException(nameof(IGreenfieldApiService) + " service is not available.");
        var appResult = await gfApiService.GetApplicationById(applicationId);
        
        return appResult.TryGetDataNonNull(out var application) 
            ? SlashCommandTypeReaderResult.Success(application) 
            : SlashCommandTypeReaderResult.Fail("The application ID requested was not found. Please ensure you have entered a valid application ID.");
    }

    public override ApplicationCommandOptionType Type => ApplicationCommandOptionType.Integer;
}