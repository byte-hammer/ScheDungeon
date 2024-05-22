using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace ScheDungeon;

class Program
{
    private const string TOKEN_ENV_NAME = "schedungeonToken";
    internal const string TEST_GUILD_ID_ENV_NAME = "schedungeonTestGuildId";

    private readonly IServiceProvider _serviceProvider;

    private readonly DiscordSocketConfig socketConfig = new()
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
        AlwaysDownloadUsers = true,
    };

    public Program()
    {
        // Setup Discord.NET service provider
        _serviceProvider = new ServiceCollection()
            .AddSingleton(socketConfig)
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton<InteractionHandler>()
            .BuildServiceProvider();
    }

    static void Main(string[] args)
    {
        new Program().RunAsync()
            .GetAwaiter()
            .GetResult();
    }

    public async Task RunAsync()
    {
        var client = _serviceProvider.GetRequiredService<DiscordSocketClient>();
        
        client.Log += LogAsync;

        // Initialize commands and interaction handler
        await _serviceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

        // Log in to Discord
        var token = Environment.GetEnvironmentVariable(TOKEN_ENV_NAME);
        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        // Keep the program running
        await Task.Delay(Timeout.Infinite);
    }

    private async Task LogAsync(LogMessage message)
    {
        Console.WriteLine(message.ToString());
    }
}