using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using ScheDungeon.EntityFramework;
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
        // Keep a list of active buttons so we can turn them off after they've been used to prevent duplicate entries
        internal Dictionary<ulong, LiveButton> LiveButtons { get; set; }
        internal ScheduledEventContext Database {  get; private set; }

        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ulong _testGuildId;

        private Task? _checkButtonTimeoutTask;
        private readonly PeriodicTimer _periodicTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider serviceProvider)
        {
            _client = client;
            _interactionService = interactionService;
            _serviceProvider = serviceProvider;

            LiveButtons = new Dictionary<ulong, LiveButton>();
            _periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            _cancellationTokenSource = new CancellationTokenSource();
            _checkButtonTimeoutTask = CheckForButtonTimeoutsAsync();

            Database = new ScheduledEventContext();

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

        public class LiveButton
        {
            public string OriginalButtonText { get; set; }
            public ulong MessageId { get; set; }
            public DateTime Timestamp { get; set; }
            public ISocketMessageChannel Channel { get; set; }

            public LiveButton(string originalButtonText, ulong messageId, DateTime timestamp, ISocketMessageChannel channel)
            {
                OriginalButtonText = originalButtonText;
                MessageId = messageId;
                Timestamp = timestamp;
                Channel = channel;
            }
        }

        // Make sure each user can only have one live button at a time.
        internal void AddLiveButton(SocketInteractionContext context, string originalButtonText)
        {
            var userId = context.User.Id;
            var messageId = context.Interaction.GetOriginalResponseAsync().Result.Id;

            if (this.LiveButtons.ContainsKey(userId))
                this.DisableLiveButton(context);

            LiveButton lb = new LiveButton(originalButtonText, messageId, DateTime.Now, context.Channel);

            this.LiveButtons.Add(userId, lb);
        }

        
        internal void DisableLiveButton(SocketInteractionContext context)
        {
            this.DisableLiveButton(context.User.Id);
        }

        // We disable the buttons if they time out or if they have been successfully used to prevent dupes.
        internal void DisableLiveButton(ulong userId)
        {
            if (this.LiveButtons.ContainsKey(userId)) 
            { 
                LiveButton lb = this.LiveButtons[userId];

                var cb = new ComponentBuilder()
                    .WithButton(lb.OriginalButtonText, "disabled_button", disabled: true);

                lb.Channel.ModifyMessageAsync(lb.MessageId, (m => m.Components = cb.Build()));
                LiveButtons.Remove(userId);
            }
        }

        // We check this timer every minute. If a button has been active for 15 minutes, disable it.
        private async Task CheckForButtonTimeoutsAsync()
        {
            try
            {
                while (await _periodicTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
                {
                    foreach (KeyValuePair<ulong, LiveButton> kvp in LiveButtons.Where(kvp => (kvp.Value.Timestamp.AddMinutes(15)) < DateTime.Now))
                    {
                        this.DisableLiveButton(kvp.Key);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
