using Discord;
using Discord.Interactions;
using ScheDungeon.Attributes;
using ScheDungeon.EntityFramework;
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

        [SlashCommand("create-event", "Create a new event that people can subscribe to for updates.")]
        public async Task CreateNewEventAsync()
        {
            var cb = new ComponentBuilder()
                .WithButton("Create Event", $"create_event_button:{Context.User.Id}");

            var eb = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle("Create a New Event")
                .WithDescription("Pressing the following button will allow you to create your event. Once it's created, people will be able to subscribe to your game for reminders, and you will be able to schedule sessions.");

            await RespondAsync(embed: eb.Build(), components: cb.Build());
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
            [ModalTextInput("event_name", placeholder: "Unnamed Event", maxLength: 64)]
            public string EventName { get; set; }

            [InputLabel("Event Description")]
            [ModalTextInput("event_description", TextInputStyle.Paragraph, placeholder: "Please enter a short description for your event.", maxLength: 500)]
            public string EventDescription { get; set; }
        }

        [ModalInteraction("create_event_modal")]
        public async Task CreateEventModalResponseAsync(CreateEventModal modal)
        {
            var eb = new EmbedBuilder();

            if (_handler.Database.ScheduledEvents.Where(se => se.Name.Equals(modal.EventName)).Any()) 
            {
                eb.WithColor(Color.Red)
                    .WithTitle("Error: Event Already Exists")
                    .WithDescription("I already have an event with this name. Did you mean to schedule a session instead? Try using `schedule-event`, or run this command again and choose a unique name.");
            }
            else
            {
                _handler.Database.Add(new ScheduledEvent
                {
                    Name = modal.EventName,
                    Description = modal.EventDescription
                });
                _handler.Database.SaveChanges();
                var scheduledEvent = _handler.Database.ScheduledEvents.Where(se => se.Name == modal.EventName).FirstOrDefault();
                if (scheduledEvent != null)
                {
                    scheduledEvent.Players.Add(new Player
                    {
                        Name = Context.User.Username
                    });
                    _handler.Database.SaveChanges();

                    eb.WithColor(Color.Green)
                        .WithTitle("New Event Created!")
                        .WithDescription("Your event has been created!")
                        .AddField("Event Name", modal.EventName)
                        .AddField("Event Description", modal.EventDescription);
                }
                else
                {
                    eb.WithColor(Color.Red)
                        .WithTitle("Error: Error writing event to database.")
                        .WithDescription("There was an error writing your event to the database. Please tell ByteHammer to check `CreateEventModalResponseAsync` for errors.");
                }
            }

            await RespondAsync(embed: eb.Build());

            _handler.DisableLiveButton(Context);
        }

        // TODO: Remove this later. This doesn't need to be a command but it makes my life easier for debugging.
        [SlashCommand("debug-clear-database", "Clears the database of all events, players, and sessions.")]
        public async Task DebugClearDatabaseAsync()
        {
            var cb = new ComponentBuilder();
            var eb = new EmbedBuilder();

            if (Context.User.Username != "bytehammer")
            {
                eb.WithColor(Color.Red)
                    .WithTitle("ERROR: You're not my boss.")
                    .WithDescription("Only ByteHammer is allowed to do this while debugging his bot. You're not supposed to see this.");

                await RespondAsync(embed: eb.Build());
            }
            else
            {
                eb.WithColor(Color.Gold)
                .WithTitle("!!!DEBUG: CLEAR DATABASE!!!")
                .WithDescription("WARNING: THIS IS IRREVERSIBLE. ALL DATA WILL BE CLEARED. PRESS BUTTON TO CONFIRM.");

                cb.WithButton("CLEAR DATABASE", "clear_database_button", style: ButtonStyle.Danger);

                await RespondAsync(embed: eb.Build(), components: cb.Build());
                _handler?.AddLiveButton(Context, "CLEAR DATABASE");
            }
        }

        // TODO: Remove this later. This doesn't need to be a command but it makes my life easier for debugging.
        [ComponentInteraction("clear_database_button")]
        public async Task ClearDatabaseButtonPressedAsync()
        {
            foreach (var se in _handler.Database.ScheduledEvents)
            {
                _handler.Database.ScheduledEvents.Remove(se);
            }

            foreach (var player in _handler.Database.Players)
            {
                _handler.Database.Players.Remove(player);
            }

            foreach (var session in _handler.Database.Sessions)
            {
                _handler.Database.Sessions.Remove(session);
            }

            _handler.Database.SaveChanges();

            var eb = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithTitle("!!!DEBUG: DATABASE CLEARED!!!")
                .WithDescription("The database is cleared and should now be empty");

            await RespondAsync(embed: eb.Build());

            _handler.DisableLiveButton(Context);
        }
    }
}
