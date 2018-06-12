using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Services;

namespace DiscordBot.Modules
{
    public class ModerationModule : ModuleBase
    {
        private readonly LoggingService _logging;
        private readonly PublisherService _publisher;
        private readonly UpdateService _update;
        private readonly UserService _user;
        private readonly DatabaseService _database;

        private Dictionary<ulong, DateTime> MutedUsers => _user.MutedUsers;

        public ModerationModule(LoggingService logging, PublisherService publisher, UpdateService update, UserService user,
            DatabaseService database)
        {
            _logging = logging;
            _publisher = publisher;
            _update = update;
            _user = user;
            _database = database;
        }

        [Command("mute"), Summary("Mute a user for a fixed duration")]
        [Alias("shutup", "stfu")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task MuteUser(IUser user, uint arg)
        {
            await Context.Message.DeleteAsync();

            var u = user as IGuildUser;
            var muteRole = Settings.GetMutedRole(Context.Guild);
            if (u != null && u.RoleIds.Contains(muteRole.Id))
            {
                return;
            }

            await u.AddRoleAsync(muteRole);

            var reply = await ReplyAsync("User " + user + " has been muted for " + arg + " seconds.");
            await _logging.LogAction($"{Context.User.Username} has muted {u.Username} for {arg} seconds");

            MutedUsers.AddCooldown(u.Id, seconds: (int) arg, ignoreExisting: true);

            await MutedUsers.AwaitCooldown(u.Id);
            await reply.DeleteAsync();
            await UnmuteUser(user, true);
        }

        [Command("mute"), Summary("Mute a user for a fixed duration")]
        [Alias("shutup", "stfu")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task MuteUser(IUser user, uint arg, string message)
        {
            await Context.Message.DeleteAsync();

            var u = user as IGuildUser;
            var muteRole = Settings.GetMutedRole(Context.Guild);
            if (u.RoleIds.Contains(muteRole.Id))
            {
                return;
            }

            await u.AddRoleAsync(muteRole);

            var reply = await ReplyAsync($"User {user} has been muted for {arg} seconds. Reason : {message}");
            await _logging.LogAction($"{Context.User.Username} has muted {u.Username} for {arg} seconds. Reason : {message}");
            var dm = await user.GetOrCreateDMChannelAsync(new RequestOptions { });

            try
            {
                await dm.SendMessageAsync($"You have been muted from UDH for **{arg}** seconds for the following reason : **{message}**. " +
                                          $"This is not appealable and any tentative to avoid it will result in your permanent ban.", false,
                    null, new RequestOptions {RetryMode = RetryMode.RetryRatelimit, Timeout = 6000});
            }
            catch (Discord.Net.HttpException)
            {
                await ReplyAsync($"Sorry {user.Mention}, seems I couldn't DM you because you blocked me !\n" +
                                 $"I'll have to send your mute reason in public :wink:\n" +
                                 $"You have been muted from UDH for **{arg}** seconds for the following reason : **{message}**. " +
                                 $"This is not appealable and any tentative to avoid it will result in your permanent ban.");
                await _logging.LogAction($"User {user.Username} has DM blocked and the mute reason couldn't be sent.", true, false);
            }

            MutedUsers.AddCooldown(u.Id, seconds: (int) arg, ignoreExisting: true);
            await MutedUsers.AwaitCooldown(u.Id);

            await UnmuteUser(user, true);
            await reply.DeleteAsync();
        }

        [Command("unmute"), Summary("Unmute a muted user")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task UnmuteUser(IUser user, bool fromMute = false)
        {
            var u = user as IGuildUser;

            if (!fromMute && Context?.Message != null)
                await Context.Message.DeleteAsync();

            MutedUsers.Remove(user.Id);
            await u.RemoveRoleAsync(Settings.GetMutedRole(Context.Guild));
            var reply = await ReplyAsync("User " + user + " has been unmuted.");
            await Task.Delay(TimeSpan.FromSeconds(10d));
            await reply.DeleteAsync();
        }

        [Command("addrole"), Summary("Add a role to a user")]
        [Alias("roleadd")]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        private async Task AddRole(IRole role, IUser user)
        {
            if (!Settings.IsRoleAssignable(role))
            {
                await ReplyAsync("Role is not assigneable");
                return;
            }

            var u = user as IGuildUser;
            await u.AddRoleAsync(role);
            await ReplyAsync("Role " + role + " has been added to " + user);
            await _logging.LogAction($"{Context.User.Username} has added role {role} to {u.Username}");
        }

        [Command("removerole"), Summary("Remove a role from a user")]
        [Alias("roleremove")]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        private async Task RemoveRole(IRole role, IUser user)
        {
            if (!Settings.IsRoleAssignable(role))
            {
                await ReplyAsync("Role is not assigneable");
                return;
            }

            var u = user as IGuildUser;

            await u.RemoveRoleAsync(role);
            await ReplyAsync("Role " + role + " has been removed from " + user);
            await _logging.LogAction($"{Context.User.Username} has removed role {role} from {u.Username}");
        }

        [Command("clear"), Summary("Remove last x messages")]
        [Alias("clean", "nuke", "purge")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task ClearMessages(int count)
        {
            var channel = Context.Channel as ITextChannel;

            var messages = await channel.GetMessagesAsync(count + 1).FlattenAsync();
            await channel.DeleteMessagesAsync(messages);

            var m = await ReplyAsync("Messages deleted.");
            await Task.Delay(5000).ContinueWith(t => {
                m.DeleteAsync();
                _logging?.LogAction($"{Context.User.Username} has removed {count} messages from {Context.Channel.Name}");
            });
        }

        [Command("kick"), Summary("Kick a user")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task KickUser(IUser user)
        {
            var u = user as IGuildUser;

            await u.KickAsync();
            await _logging.LogAction($"{Context.User.Username} has kicked {u.Username}");
        }

        [Command("ban"), Summary("Ban an user")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task BanUser(IUser user, string reason)
        {
            await Context.Guild.AddBanAsync(user, 7, reason, RequestOptions.Default);
            await _logging.LogAction($"{Context.User.Username} has banned {user.Username}");
        }

        [Command("debug"), Summary("Debug")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task Debug(IUser user)
        {
            var guildUser = (IGuildUser) user;
            await ReplyAsync(guildUser.RoleIds.Count.ToString());
        }

        [Command("rules"), Summary("Display rules of the current channel.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task Rules(int seconds = 60)
        {
            await Rules(Context.Channel, seconds);
            await Context.Message.DeleteAsync();
        }

        [Command("rules"), Summary("Display rules of the mentionned channel.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task Rules(IMessageChannel channel, int seconds = 60)
        {
            //Display rules of this channel for x seconds
            var rule = Settings.GetRule(channel.Id);
            IUserMessage m;
            var dm = await Context.User.GetOrCreateDMChannelAsync();
            if (rule == null)
                m = await ReplyAsync(
                    "There is no special rule for this channel.\nPlease follow global rules (you can get them by typing `!globalrules`)");
            else
            {
                m = await ReplyAsync(
                    $"{rule.header}{(rule.content.Length > 0 ? rule.content : "There is no special rule for this channel.\nPlease follow global rules (you can get them by typing `!globalrules`)")}");
            }

            var deleteAsync = Context.Message?.DeleteAsync();
            if (deleteAsync != null) await deleteAsync;

            if (seconds == -1)
                return;
            await Task.Delay(seconds * 1000);
            await m.DeleteAsync();
        }

        [Command("globalrules"), Summary("Display globalrules in current channel.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task GlobalRules(int seconds = 60)
        {
            //Display rules of this channel for x seconds
            var globalRules = Settings.GetRule(0).content;
            var m = await ReplyAsync(globalRules);
            await Context.Message.DeleteAsync();

            if (seconds == -1)
                return;
            await Task.Delay(seconds * 1000);
            await m.DeleteAsync();
        }

        [Command("channels"), Summary("Get a description of the channels.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task ChannelsDescription(int seconds = 60)
        {
            //Display rules of this channel for x seconds
            var headers = Settings.GetChannelsHeader();
            var sb = new StringBuilder();

            foreach (var h in headers)
                sb.Append($"{(await Context.Guild.GetTextChannelAsync(h.Item1))?.Mention} - {h.Item2}\n");
            var text = sb.ToString();
            IUserMessage m;
            IUserMessage m2 = null;

            if (sb.ToString().Length > 2000)
            {
                m = await ReplyAsync(text.Substring(0, 2000));
                m2 = await ReplyAsync(text.Substring(2000));
            }
            else
            {
                m = await ReplyAsync(text);
            }

            await Context.Message.DeleteAsync();

            if (seconds == -1)
                return;
            await Task.Delay(seconds * 1000);
            await m.DeleteAsync();
            var deleteAsync = m2?.DeleteAsync();
            if (deleteAsync != null) await deleteAsync;
        }

        [Command("ad"), Summary("Post ad with databaseid")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task PostAd(uint dbId)
        {
            await _publisher.PostAd(dbId);
            await ReplyAsync("Ad posted.");
        }

        [Command("forcead"), Summary("Force post ad")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task ForcePostAd()
        {
            await _update.CheckDailyPublisher(true);
            await ReplyAsync("New ad posted.");
        }

        [Command("dbsync"), Summary("Force add user to database")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        private async Task DbSync(IUser user) => _database.AddNewUser((SocketGuildUser) user);
    }
}