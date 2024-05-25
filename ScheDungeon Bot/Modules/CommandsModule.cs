using Discord;
using Discord.Interactions;
using ScheDungeon.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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

        [SlashCommand("create-event", "Create a new event that people can subscribe to for updates.")]
        public async Task CreateNewEventAsync()
        {
            var cb = new ComponentBuilder()
                .WithButton("Create Event", $"create_event_button:{Context.User.Id}");

            var eb = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle("Create a New Event")
                .WithDescription("Pressing the following button will allow you to create your event. Once it's created, people will be able to subscribe to your game for reminders, and you will be able to schedule sessions.");

            await RespondAsync(embed: eb.Build(), components:cb.Build());
            _handler.AddLiveButton(Context, "Create Event");
        }

        [DoUserCheck]
        [ComponentInteraction("create_event_button:*")]
        public async Task CreateEventButtonPressedAsync()
        {
            await Context.Interaction.RespondWithModalAsync<CreateEventModal>("create_event_modal");
        }

        public class CreateEventModal : IModal
        {
            public string Title => "Create an Event";

            [InputLabel("Event Name")]
            [ModalTextInput("event_name", placeholder: "Unnamed Event", maxLength:64)]
            public string EventName { get; set; }

            [InputLabel("Event Description")]
            [ModalTextInput("event_description", TextInputStyle.Paragraph, placeholder: "Please enter a short description for your event.", maxLength: 500)]
            public string EventDescription { get; set; }
        }

        [ModalInteraction("create_event_modal")]
        public async Task CreateEventModalResponseAsync(CreateEventModal modal)
        {
            var eb = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("New Event Created!")
                .WithDescription("I received your response to the 'Create an Event' dialog box. I don't store any info yet, but here's the data I received:")
                .AddField("Event Name", modal.EventName)
                .AddField("Event Description", modal.EventDescription);

            await RespondAsync(embed: eb.Build());

            _handler.DisableLiveButton(Context);
        }
    }
}
