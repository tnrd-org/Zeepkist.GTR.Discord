using Microsoft.Extensions.Options;
using Remora.Commands.Extensions;
using Remora.Discord.API;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway;
using Remora.Discord.Hosting.Extensions;
using Remora.Rest.Core;
using Remora.Results;
using Serilog;
using TNRD.Zeepkist.GTR.Discord;
using TNRD.Zeepkist.GTR.Discord.Commands;

internal class Program
{
    /// <summary>
    /// The main entrypoint of the program.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous program execution.</returns>
    public static async Task Main(string[] args)
    {
        IHost host = CreateHostBuilder(args)
            .UseConsoleLifetime()
            .Build();

        IServiceProvider services = host.Services;
        ILogger<Program> log = services.GetRequiredService<ILogger<Program>>();

        Snowflake? debugServer = null;

#if DEBUG
        string? debugServerString = services.GetRequiredService<IOptions<DiscordOptions>>().Value.DebugServer;
        if (debugServerString is not null)
        {
            if (!DiscordSnowflake.TryParse(debugServerString, out debugServer))
            {
                log.LogWarning("Failed to parse debug server from environment");
            }
        }
#endif

        SlashService slashService = services.GetRequiredService<SlashService>();
        Result updateSlash = await slashService.UpdateSlashCommandsAsync(debugServer);
        if (!updateSlash.IsSuccess)
        {
            log.LogWarning("Failed to update slash commands: {Reason}", updateSlash.Error.Message);
        }

        await host.RunAsync();
    }

    /// <summary>
    /// Creates a generic application host builder.
    /// </summary>
    /// <param name="args">The arguments passed to the application.</param>
    /// <returns>The host builder.</returns>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((context, configuration) =>
            {
                configuration
                    .MinimumLevel.Debug()
                    .WriteTo.Console();
            })
            .AddDiscordService
            (
                services =>
                {
                    DiscordOptions options = services.GetRequiredService<IOptions<DiscordOptions>>().Value;
                    return options.Token ?? throw new InvalidOperationException("No bot token has been provided.");
                }
            )
            .ConfigureServices
            (
                (context, services) =>
                {
                    services.Configure<DiscordOptions>(context.Configuration.GetSection("Discord"));

                    services.AddHttpClient();
                    services.Configure<DiscordGatewayClientOptions>(g => { });

                    services.AddHostedService<WebSocketHostedService>();
                    services.AddSingleton<DiscordMessageSender>();

                    services
                        .AddDiscordCommands(true)
                        .AddCommandTree()
                        .WithCommandGroup<InteractiveCommands>()
                        .Finish();
                }
            )
            .ConfigureLogging
            (
                c => c
                    .AddConsole()
                    .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                    .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
            );
    }
}
