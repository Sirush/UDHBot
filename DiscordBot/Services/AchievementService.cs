using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Settings.Deserialized;

namespace DiscordBot.Services {
    public class AchievementService {
        private readonly DatabaseService _databaseService;
        
        private Achievement[] _xpAchievements;
        private Achievement[] _karmaAchievements;

        public AchievementService(DatabaseService databaseService) {
            _databaseService = databaseService;
        }

        private Achievement[] GetAchievementsWithRequirement(Achievement[] achievements, String requirement) {
            List<Achievement> currentAchievements = new List<Achievement>();

            foreach (Achievement ach in achievements) {
                if (ach.requirement == requirement) {
                    currentAchievements.Add(ach);
                }
            }

            return currentAchievements.ToArray();
        }

        //Sort the achievements into category's so it does not need to loop over every achievement when a user speaks
        public void LoadAchievements(Achievement[] achievements) {
            _xpAchievements = GetAchievementsWithRequirement(achievements, "Level");
            _karmaAchievements = GetAchievementsWithRequirement(achievements, "KarmaGained");
        }
        
        public void ShowEarnedAchievement(String username, Achievement achievement, IMessageChannel channel) {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.Title = $"{username} just earned the achievement, {achievement.name}!";
            embedBuilder.Description = achievement.description;

            channel.SendMessageAsync("", false, embedBuilder.Build()).DeleteAfterTime(minutes: 10);
        }

        public void OnLevelUp(SocketMessage message, int level) {
            Achievement[] userAchievements = _databaseService.GetUserAchievements(message.Author.Id);
            
            foreach (Achievement achievement in _xpAchievements) {
                if (level >= Int64.Parse(achievement.value)) {
                    //Make sure they don't already have it
                    if (!userAchievements.Contains(achievement)) {
                        //Grant achievement
                        _databaseService.AddUserAchievement(achievement, message.Author.Id);
                        ShowEarnedAchievement(message.Author.Username, achievement, message.Channel);
                    }
                }
            }

            CheckUserRank(message, userAchievements);
        }
        
        public void OnGainKarma(SocketUser user, SocketMessage message) {
            Achievement[] userAchievements = _databaseService.GetUserAchievements(user.Id);

            int karma = _databaseService.GetUserKarma(user.Id);
            
            foreach (Achievement achievement in _karmaAchievements) {
                if (karma >= Int64.Parse(achievement.value)) {
                    //Make sure they don't already have it
                    if (!userAchievements.Contains(achievement)) {
                        //Grant achievement
                        _databaseService.AddUserAchievement(achievement, user.Id);
                        ShowEarnedAchievement(user.Username, achievement, message.Channel);
                    }
                }
            }
        }

        private void CheckUserRank(SocketMessage message, Achievement[] userAchievements) {
            uint rank = _databaseService.GetUserRank(message.Author.Id);
            
            foreach (Achievement achievement in _xpAchievements) {
                if (rank <= Int64.Parse(achievement.value)) {
                    //Make sure they dont already have it
                    if (!userAchievements.Contains(achievement)) {
                        //Grant achievement
                        _databaseService.AddUserAchievement(achievement, message.Author.Id);
                        ShowEarnedAchievement(message.Author.Username, achievement, message.Channel);
                    }
                }
            }
        }
    }
}