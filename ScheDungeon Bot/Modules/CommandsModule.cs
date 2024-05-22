using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScheDungeon.Modules
{
    public class CommandsModule : InteractionModuleBase<SocketInteractionContext>
    {
        // BH: Comment below not mine. If I understand this correctly, the service provider sets this for me.
        // Dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        public InteractionService Commands { get; set; }

        private InteractionHandler _handler { get; set; }

        public CommandsModule(InteractionHandler handler)
        {
            _handler = handler;
        }

        [SlashCommand("latency", "Debug message to test if my commands are being registered.")]
        public async Task PingAsync()
            => await RespondAsync(text: $"DEBUG: Latency is {Context.Client.Latency}ms");

        [SlashCommand("latency-ephemeral", "Debug message to test if my commands are being registered. Ephemeral version.")]
        public async Task PingEphemeralAsync()
            => await RespondAsync(text: $"DEBUG: Latency is {Context.Client.Latency}ms. Ephemeral", ephemeral: true);
    }
}
