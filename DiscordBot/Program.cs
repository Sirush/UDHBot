﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Services;
using DiscordBot.Settings.Deserialized;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using IMessage = Discord.IMessage;

namespace DiscordBot
{
    public class Program
    {
        public static string CommandList;

        private DiscordSocketClient _client;

        private CommandService _commandService;
        private IServiceProvider _services;
        private ILoggingService _loggingService;
        private DatabaseService _databaseService;
        private UserService _userService;

        private static PayWork _payWork;
        private static Rules _rules;
        private static Settings.Deserialized.Settings _settings;
        private static UserSettings _userSettings;

        public static void Main(string[] args) =>
            new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            DeserializeSettings();

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose, AlwaysDownloadUsers = true, MessageCacheSize = 50
            });
            
            _commandService = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async
            });

            _services = ConfigureServices();
            _loggingService = _services.GetRequiredService<ILoggingService>();
            _databaseService = _services.GetRequiredService<DatabaseService>();
            _userService = _services.GetRequiredService<UserService>();

            _client.Log += Logger;

            await _client.LoginAsync(TokenType.Bot, _settings.Token);
            await _client.StartAsync();
            
            await InstallCommands();

            _client.Ready += () =>
            {
                Console.WriteLine("Bot is connected");
                //_audio.Music();
                return Task.CompletedTask;
            };

            await Task.Delay(-1);
        }
        
        private IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(_settings)
                .AddSingleton(_rules)
                .AddSingleton(_payWork)
                .AddSingleton(_userSettings)
                .AddSingleton(_client)
                .AddSingleton(_commandService)
                .AddSingleton<ILoggingService, LoggingService>()
                .AddSingleton<DatabaseService>()
                .AddSingleton<UserService>()
                .AddSingleton<PublisherService>()
                .AddSingleton<FeedService>()
                .AddSingleton<UpdateService>()
                .AddSingleton<AudioService>()
                .AddSingleton<AnimeService>()
                .AddSingleton<CurrencyService>()
                .BuildServiceProvider();
        }

        private static Task Logger(LogMessage message)
        {
            ConsoleColor cc = Console.ForegroundColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }

            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}");
            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }

        public async Task InstallCommands()
        {
            //_client.MessageReceived += _work.OnMessageAdded;
            _client.MessageDeleted += MessageDeleted;
            _client.UserJoined += UserJoined;
            _client.GuildMemberUpdated += UserUpdated;
            _client.UserLeft += UserLeft;

            // Discover all of the commands in this assembly and load them.
            await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            StringBuilder commandList = new StringBuilder();

            commandList.Append("__Role Commands__\n");
            foreach (var c in _commandService.Commands.Where(x => x.Module.Name == "role").OrderBy(c => c.Name))
            {
                commandList.Append($"**role {c.Name}** : {c.Summary}\n");
            }
            
            commandList.Append("\n");
            commandList.Append("__General Commands__\n");
            
            foreach (var c in _commandService.Commands.Where(x => x.Module.Name == "UserModule").OrderBy(c => c.Name))
            {
                commandList.Append($"**{c.Name}** : {c.Summary}\n");
            }

            CommandList = commandList.ToString();
        }

        private async Task MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            if (message.Value.Author.IsBot || channel.Id == _settings.BotAnnouncementChannel.Id)
                return;

            var content = message.Value.Content;
            if (content.Length > 800)
                content = content.Substring(0, 800);

            EmbedBuilder builder = new EmbedBuilder()
                .WithColor(new Color(200, 128, 128))
                .WithTimestamp(message.Value.Timestamp)
                .WithFooter(footer =>
                {
                    footer
                        .WithText($"In channel {message.Value.Channel.Name}");
                })
                .WithAuthor(author =>
                {
                    author
                        .WithName($"{message.Value.Author.Username}");
                })
                .AddField("Deleted message", content);
            Embed embed = builder.Build();

            await _loggingService.LogAction(
                $"User {message.Value.Author.Username}#{message.Value.Author.DiscriminatorValue} has " +
                $"deleted the message\n{content}\n from channel #{channel.Name}", true, false);
            await _loggingService.LogAction(" ", false, true, embed);
        }

        private async Task UserJoined(SocketGuildUser user)
        {
            ulong general = _settings.GeneralChannel.Id;
            var socketTextChannel = _client.GetChannel(general) as SocketTextChannel;

            _databaseService.AddNewUser(user);

            //Check for existing mute
            if (_userService._mutedUsers.HasUser(user.Id))
            {
                await user.AddRoleAsync(socketTextChannel?.Guild.GetRole(_settings.MutedRoleId));
                await _loggingService.LogAction(
                    $"Currently muted user rejoined - {user.Mention} - `{user.Username}#{user.DiscriminatorValue}` - ID : `{user.Id}`");
                await socketTextChannel.SendMessageAsync(
                    $"{user.Mention} tried to rejoin the server to avoid their mute. Mute time increased by 72 hours.");
                _userService._mutedUsers.AddCooldown(user.Id, hours: 72);
                return;
            }


            await _loggingService.LogAction(
                $"User Joined - {user.Mention} - `{user.Username}#{user.DiscriminatorValue}` - ID : `{user.Id}`");

            Embed em = _userService.WelcomeMessage(user.GetAvatarUrl(), user.Username, user.DiscriminatorValue);

            if (socketTextChannel != null)
            {
                await socketTextChannel.SendMessageAsync(string.Empty, false, em);
            }

            string globalRules = _rules.Channel.First(x => x.Id == 0).Content;
            IDMChannel dm = await user.GetOrCreateDMChannelAsync();
            await dm.SendMessageAsync(
                "Hello and welcome to Unity Developer Community !\nHope you enjoy your stay.\nHere are some rules to respect to keep the community friendly, please read them carefully.\n" +
                "Please also read the additional informations in the **#welcome** channel." +
                "You can get all the available commands on the server by typing !help in the **#bot-commands** channel.");
            await dm.SendMessageAsync(globalRules);

            //TODO: add users when bot was offline
        }

        private async Task UserUpdated(SocketGuildUser oldUser, SocketGuildUser user)
        {
            if (oldUser.Nickname != user.Nickname)
            {
                await _loggingService.LogAction(
                    $"User {oldUser.Nickname ?? oldUser.Username}#{oldUser.DiscriminatorValue} changed his " +
                    $"username to {user.Nickname ?? user.Username}#{user.DiscriminatorValue}");
                _databaseService.UpdateUserName(user.Id, user.Nickname);
            }

            if (oldUser.AvatarId != user.AvatarId)
            {
                var avatar = user.GetAvatarUrl();
                _databaseService.UpdateUserAvatar(user.Id, avatar);
            }
        }

        private async Task UserLeft(SocketGuildUser user)
        {
            DateTime joinDate;
            DateTime.TryParse(_databaseService.GetUserJoinDate(user.Id), out joinDate);
            TimeSpan timeStayed = DateTime.Now - joinDate;
            await _loggingService.LogAction(
                $"User Left - After {(timeStayed.Days > 1 ? Math.Floor((double) timeStayed.Days).ToString() + " days" : " ")}" +
                $" {Math.Floor((double) timeStayed.Hours).ToString()} hours {user.Mention} - `{user.Username}#{user.DiscriminatorValue}` - ID : `{user.Id}`");
            _databaseService.DeleteUser(user.Id);
        }
        

        private static void DeserializeSettings()
        {
            using (var file = File.OpenText(@"Settings/Settings.json"))
            {
                _settings = JsonConvert.DeserializeObject<Settings.Deserialized.Settings>(file.ReadToEnd());
            }

            using (var file = File.OpenText(@"Settings/PayWork.json"))
            {
                _payWork = JsonConvert.DeserializeObject<PayWork>(file.ReadToEnd());
            }

            using (var file = File.OpenText(@"Settings/Rules.json"))
            {
                _rules = JsonConvert.DeserializeObject<Rules>(file.ReadToEnd());
            }

            using (var file = File.OpenText(@"Settings/UserSettings.json"))
            {
                _userSettings = JsonConvert.DeserializeObject<UserSettings>(file.ReadToEnd());
            }
        }
    }
}