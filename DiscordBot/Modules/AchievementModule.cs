using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
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

        public AchievementModule (Settings.Deserialized.Settings settings, DatabaseService databaseService, Achievements achievements) {
            _settings = settings;
            _databaseService = databaseService;
            _achievements = achievements;
        }
        
        [Command("Achievements"), Alias("ach", "achievement"), Summary("Lists your achievements")]
        private async Task ShowAchievements() {
            Achievement[] achievements = _databaseService.GetUserAchievements(Context.Message.Author.Id);

            String message = "You have the following achievements. ```";
            foreach (Achievement ach in achievements) {
                message += ach.description + '\n';
            }

            message += "```";
            await ReplyAsync(message);
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
        }
        
    }
}