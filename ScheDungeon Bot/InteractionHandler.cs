using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ScheDungeon
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ulong _testGuildId;

        public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider serviceProvider)
        {
            _client = client;
            _interactionService = interactionService;
            _serviceProvider = serviceProvider;

            // When testing, it's extremely inconvenient that registering a new global command can take up to an hour.
            // So, when debugging, we'll register them as commands for a specific server, or "guild", which is fast.
#if DEBUG
            ulong testGuildId = 0;
            var testGuildIdString = Environment.GetEnvironmentVariable(Program.TEST_GUILD_ID_ENV_NAME);
            if (!String.IsNullOrEmpty(testGuildIdString) && ulong.TryParse(testGuildIdString, out testGuildId))
            {
                _testGuildId = testGuildId;
            }
#endif
        }

        // Things to do here
        // 1 - Get test guild ID if we're in Debug
        // 2 - Hook up the logger and ReadyAsync callback
        // 3 - Add modules from this assembly to interaction service
        // 4 - Hook up our interaction handler
        public async Task InitializeAsync()
        {
            _client.Ready += ReadyAsync;
            _interactionService.Log += LogAsync;

            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);

            _client.InteractionCreated += HandleInteraction;
        }

        private async Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
        }

        private async Task ReadyAsync()
        {
#if DEBUG
            await _interactionService.RegisterCommandsToGuildAsync(_testGuildId, true);
#else
            await _interactionService.RegisterCommandsGloballyAsync(true);
#endif
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider);

                if (!result.IsSuccess)
                {
                    switch (result.Error)
                    {
                        case InteractionCommandError.UnmetPrecondition:
                            // TODO: implement these cases
                            break;
                        default:
                            break;
                    }
                }
            }
            catch
            {
                // FROM SAMPLE
                // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
                // response, or at least let the user know that something went wrong during the command execution.
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }
    }
}
