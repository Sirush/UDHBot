using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Services;
using DiscordBot.Settings.Deserialized;

// ReSharper disable all UnusedMember.Local
namespace DiscordBot.Modules
{
    public class AchievementModule : ModuleBase {
        private Settings.Deserialized.Settings _settings;
        private readonly DatabaseService _databaseService;
        private readonly Achievements _achievements;
        private readonly AchievementService _achievementService;
        
        

        public AchievementModule (Settings.Deserialized.Settings settings, DatabaseService databaseService, AchievementService achievementService, Achievements achievements) {
            _settings = settings;
            _databaseService = databaseService;
            _achievements = achievements;
            _achievementService = achievementService;
            
            achievementService.LoadAchievements(_achievements.Achievement.ToArray());
        }
        
        [Command("Achievements"), Alias("ach", "achievement"), Summary("Lists your achievements")]
        private async Task ShowAchievements() {
            Achievement[] achievements = _databaseService.GetUserAchievements(Context.Message.Author.Id);

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.Title = $"{Context.User.Username}'s achievements";

            
            foreach (Achievement ach in achievements) {
                embedBuilder.AddField("- " + ach.name, ach.description, true);
            }

            await Context.Channel.SendMessageAsync("", false, embedBuilder.Build()).DeleteAfterTime(minutes: 10);
        }
        
        [Command("GiveAchievement"), Alias("giveach"), Summary("Gives user an achievement")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task GiveAchievement(String achId, IGuildUser user) {
            Achievement[] achievements = _databaseService.GetUserAchievements(Context.Message.Author.Id);

            Achievement ach = null;
            
            //Loop through achievements to find matching id
            foreach (var achievement in _achievements.Achievement) {
                if (achId.ToLower() == achievement.id.ToLower()) {
                    ach = achievement;
                }
            }

            if (ach == null) {
                await ReplyAsync("Could not find achievement ID");
                return;
            }
            
            _databaseService.AddUserAchievement(ach, user.Id);
            
            await ReplyAsync("Added achievement");

            _achievementService.ShowEarnedAchievement(user.Username, ach, Context.Channel);
        }
    }
}