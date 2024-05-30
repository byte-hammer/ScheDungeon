using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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

        // //////////////////////////////////////////////////////////////////////////////////////// //
        // CREATE-EVENT WORKFLOW                                                                    //
        // //////////////////////////////////////////////////////////////////////////////////////// //
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

            [InputLabel("Custom Role Name")]
            [ModalTextInput("event_role_name", placeholder: "Unnamed Event Role", maxLength: 32)]
            public string EventRoleName { get; set; }
        }

        [ModalInteraction("create_event_modal")]
        public async Task CreateEventModalResponseAsync(CreateEventModal modal)
        {
            var eb = new EmbedBuilder();

            // Check to see if an event with this name already exists
            if (_handler.Database.ScheduledEvents.Where(se => se.Name.Equals(modal.EventName)).Any())
            {
                eb.WithColor(Color.Red)
                    .WithTitle("Error: Event Already Exists")
                    .WithDescription("An event with this name already exists. Did you mean to schedule a session instead? Try using `schedule-event`, or run this command again and choose a unique name.");
            }
            else
            {
                // Create custom role for event subscribers and give it to event owner
                var newRole = await Context.Guild.CreateRoleAsync(modal.EventRoleName, color: Color.Blue, isMentionable: true);
                await ((IGuildUser)Context.User).AddRoleAsync(newRole);

                // Create a database entry for the event
                _handler.Database.Add(new ScheduledEvent
                {
                    Name = modal.EventName,
                    Description = modal.EventDescription,
                    CustomRoleId = newRole.Id,
                });
                _handler.Database.SaveChanges();

                var scheduledEvent = _handler.Database.ScheduledEvents.Where(se => se.Name == modal.EventName).FirstOrDefault();
                if (scheduledEvent != null)
                {
                    eb.WithColor(Color.Green)
                        .WithTitle("New Event Created!")
                        .WithDescription("Your event has been created!")
                        .AddField("Event Name", modal.EventName)
                        .AddField("Event Description", modal.EventDescription)
                        .AddField("Event Role", modal.EventRoleName);
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
        // //////////////////////////////////////////////////////////////////////////////////////// //

        // //////////////////////////////////////////////////////////////////////////////////////// //
        // SUBSCRIBE-TO-EVENT WORKFLOWS                                                             //
        // //////////////////////////////////////////////////////////////////////////////////////// //
        [SlashCommand("subscribe-to-event", "Select one or more event roles to give yourself for event updates and notifications.")]
        public async Task SubscribeToEventAsync()
        {
            // Check to see if we even have any events to subscribe to.
            if (!_handler.Database.ScheduledEvents.Any())
            {
                var eb = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("Error: No Events")
                    .WithDescription("There are no events to subscribe to! Create one with `create-event`.");

                await RespondAsync(embed: eb.Build());
                return;
            }

            // Build a role select menu with all of the event roles in the database
            var cb = new ComponentBuilder();
            var mb = new SelectMenuBuilder();

            mb.WithPlaceholder("Select an event role.")
                .WithCustomId($"event_subscribe_menu:{Context.User.Id}");

            foreach (var se in _handler.Database.ScheduledEvents)
            {
                var role = Context.Guild.GetRole(se.CustomRoleId);
                if (role != null && Context.User is SocketGuildUser user && !user.Roles.Any(r => r == role))
                {
                    mb.AddOption(role.Name, role.Id.ToString());
                }
            }

            // Check if there are any event roles available for the user.
            if (mb.Options.Any()) 
            {
                cb.WithSelectMenu(mb);
                await RespondAsync(components: cb.Build());
            }
            else
            {
                var eb = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("No available event roles!")
                    .WithDescription("You are subscribed to all the events! There's nothing else to subscribe to.");

                await RespondAsync(embed: eb.Build());
            }
        }

        [DoUserCheck]
        [ComponentInteraction("event_subscribe_menu:*")]
        public async Task AddEventRoleToUserAsync(string userId, string[] selections)
        {
            var eb = new EmbedBuilder();
            
            if (selections.Any())
            {
                ulong roleId;
                UInt64.TryParse(selections.First(), out roleId);
                var eventRole = Context.Guild.GetRole(roleId);
                await ((IGuildUser)Context.User).AddRoleAsync(eventRole);

                eb.WithColor(Color.Green)
                    .WithTitle($"Role Added: {eventRole.Name}")
                    .WithDescription($"You have been assigned the event role {eventRole.Name}");
            }
            else
            {
                eb.WithColor(Color.Red)
                    .WithTitle("Error: Selections are null.")
                    .WithDescription("There was an error processing your role selection. Tell ByteHammer to check `AddEventRolesToUserAsync`");
            }

            await RespondAsync(embed: eb.Build());
        }
        // //////////////////////////////////////////////////////////////////////////////////////// //

        // //////////////////////////////////////////////////////////////////////////////////////// //
        // UNSUBSCRIBE-TO-EVENT WORKFLOWS                                                           //
        // //////////////////////////////////////////////////////////////////////////////////////// //
        [SlashCommand("unsubscribe-from-event", "Unsubscribe from an event. You will no longer receive automated pings from the selected events.")]
        public async Task UnsubscribeFromEventAsync()
        {
            // Check to see if we even have any events to unsubscribe to.
            if (!_handler.Database.ScheduledEvents.Any())
            {
                var eb = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("Error: No Events")
                    .WithDescription("There are no events to unsubscribe from!");

                await RespondAsync(embed: eb.Build());
                return;
            }

            // Build a role select menu with all of the event roles in the database that the user is subscribed to
            var cb = new ComponentBuilder();
            var mb = new SelectMenuBuilder();

            mb.WithPlaceholder("Select an event role.")
                .WithCustomId($"event_unsubscribe_menu:{Context.User.Id}");

            foreach (var se in _handler.Database.ScheduledEvents)
            {
                var role = Context.Guild.GetRole(se.CustomRoleId);
                if (role != null && Context.User is SocketGuildUser user && user.Roles.Any(r => r == role))
                {
                    mb.AddOption(role.Name, role.Id.ToString());
                }
            }

            // Check if there are any event roles available for the user.
            if (mb.Options.Any())
            {
                cb.WithSelectMenu(mb);
                await RespondAsync(components: cb.Build());
            }
            else
            {
                var eb = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("No event roles assigned!")
                    .WithDescription("You are unsubscribed from all the events! There's nothing else to unsubscribe from.");

                await RespondAsync(embed: eb.Build());
            }
        }

        [DoUserCheck]
        [ComponentInteraction("event_unsubscribe_menu:*")]
        public async Task RemoveEventRoleFromUserAsync(string userId, string[] selections)
        {
            var eb = new EmbedBuilder();

            if (selections.Any())
            {
                ulong roleId;
                UInt64.TryParse(selections.First(), out roleId);
                var eventRole = Context.Guild.GetRole(roleId);
                await ((IGuildUser)Context.User).RemoveRoleAsync(eventRole);

                eb.WithColor(Color.Green)
                    .WithTitle($"Role Removed: {eventRole.Name}")
                    .WithDescription($"You have been unassigned the event role {eventRole.Name}");
            }
            else
            {
                eb.WithColor(Color.Red)
                    .WithTitle("Error: Selections are null.")
                    .WithDescription("There was an error processing your role selection. Tell ByteHammer to check `RemoveEventRoleFromUserAsync`");
            }

            await RespondAsync(embed: eb.Build());
        }
        // //////////////////////////////////////////////////////////////////////////////////////// //

        // //////////////////////////////////////////////////////////////////////////////////////// //
        // DEBUG WORKFLOWS                                                                          //
        // //////////////////////////////////////////////////////////////////////////////////////// //
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

        [ComponentInteraction("clear_database_button")]
        public async Task ClearDatabaseButtonPressedAsync()
        {
            foreach (var se in _handler.Database.ScheduledEvents)
            {
                _handler.Database.ScheduledEvents.Remove(se);
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
        // //////////////////////////////////////////////////////////////////////////////////////// //
    }
}
