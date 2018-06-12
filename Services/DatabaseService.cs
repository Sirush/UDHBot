﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using MySql.Data.MySqlClient;

namespace DiscordBot.Services
{
    public class DatabaseService
    {
        private string Connection { get; }

        private readonly LoggingService _logging;

        public DatabaseService(LoggingService logging)
        {
            Connection = SettingsHandler.LoadValueString("dbConnectionString", JsonFile.Settings);
            _logging = logging;
        }

        /*
        **Publisher Stuff
        */
        public uint GetPublisherAdCount()
        {
            using (var connection = new MySqlConnection(Connection))
            {
                var command = new MySqlCommand("SELECT COUNT(*) FROM advertisment", connection);
                connection.Open();
                return Convert.ToUInt32(command.ExecuteScalar());
            }
        }

        public (uint pkgId, ulong userId) GetPublisherAd(uint id)
        {
            using (var connection = new MySqlConnection(Connection))
            {
                var command = new MySqlCommand($"Select username, userid, packageID FROM advertisment WHERE id='{id}'", connection);
                connection.Open();
                MySqlDataReader reader;
                using (reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        return (Convert.ToUInt32(reader["packageID"]), Convert.ToUInt64(reader["userid"]));
                    }
                }
            }

            return (0, 0);
        }

        public void AddPublisherPackage(string username, string discriminator, string userid, uint packageId)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand(
                        $"INSERT INTO advertisment SET username='{username}', discriminator='{discriminator}', userid='{userid}', packageID='{packageId}', date='{DateTime.Now:yyyy-MM-dd HH:mm:ss}'",
                        connection);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e) {
                _logging?.LogAction($"Error when trying to add package {packageId} from {username}#{discriminator} - {userid} : {e}");
            }
        }

        /*
        Update Service
        */
        public void UpdateUserRanks()
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand(
                        "SET @prev_value = NULL; SET @rank_count = 0; " +
                        "UPDATE users SET rank = @rank_count := IF(@prev_value = rank, @rank_count, @rank_count + 1) " +
                        "ORDER BY exp DESC", connection);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e) {
                _logging?.LogAction($"Error when trying to update ranks : {e}", true, false);
            }
        }


        public void AddUserXp(ulong id, int xp)
        {
            var reader = GetAttributeFromUser(id, "exp");

            var oldXp = Convert.ToInt32(reader);
            UpdateAttributeFromUser(id, "exp", oldXp + xp);
        }

        public void AddUserLevel(ulong id, uint level)
        {
            var reader = GetAttributeFromUser(id, "level");

            var oldLevel = Convert.ToUInt32(reader);
            UpdateAttributeFromUser(id, "level", oldLevel + level);
        }

        public void AddUserKarma(ulong id, int karma)
        {
            var reader = GetAttributeFromUser(id, "karma");

            var oldKarma = Convert.ToInt32(reader);
            UpdateAttributeFromUser(id, "karma", oldKarma + karma);
        }

        public uint GetUserXp(ulong id)
        {
            var reader = GetAttributeFromUser(id, "exp");

            var xp = Convert.ToUInt32(reader);

            return xp;
        }

        public int GetUserKarma(ulong id)
        {
            var reader = GetAttributeFromUser(id, "karma");

            var karma = Convert.ToInt32(reader);

            return karma;
        }

        public uint GetUserRank(ulong id)
        {
            var reader = GetAttributeFromUser(id, "rank");

            var rank = Convert.ToUInt32(reader);

            return rank;
        }

        public uint GetUserLevel(ulong id)
        {
            var reader = GetAttributeFromUser(id, "level");

            var level = Convert.ToUInt32(reader);

            return level;
        }

        public string GetUserJoinDate(ulong id) => GetAttributeFromUser(id, "joinDate");

        public void UpdateUserName(ulong id, string name)
        {
            UpdateAttributeFromUser(id, "username", name);
        }

        public void UpdateUserAvatar(ulong id, string avatar)
        {
            UpdateAttributeFromUser(id, "avatarUrl", avatar);
        }

        public void AddUserUdc(ulong id, int udc)
        {
            var reader = GetAttributeFromUser(id, "udc");

            var oldUdc = Convert.ToInt32(reader);
            UpdateAttributeFromUser(id, "udc", oldUdc + udc);
        }

        public int GetUserUdc(ulong id) => Convert.ToInt32(GetAttributeFromUser(id, "udc"));

        public List<(ulong userId, int level)> GetTopLevel()
        {
            var users = new List<(ulong userId, int level)>();

            using (var connection = new MySqlConnection(Connection))
            {
                var command = new MySqlCommand("SELECT userid, level FROM `users` ORDER BY exp DESC LIMIT 10", connection);
                connection.Open();
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add((reader.GetUInt64(0), reader.GetInt32(1)));
                }
            }


            return users;
        }

        public List<(ulong userId, int karma)> GetTopKarma()
        {
            var users = new List<(ulong userId, int karma)>();

            using (var connection = new MySqlConnection(Connection))
            {
                var command = new MySqlCommand("SELECT userid, karma FROM `users` ORDER BY karma DESC LIMIT 10", connection);
                connection.Open();
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add((reader.GetUInt64(0), reader.GetInt32(1)));
                }
            }

            return users;
        }

        public List<(ulong userId, int udc)> GetTopUdc()
        {
            var users = new List<(ulong userId, int udc)>();

            using (var connection = new MySqlConnection(Connection))
            {
                var command = new MySqlCommand("SELECT userid, udc FROM `users` ORDER BY udc DESC LIMIT 10", connection);
                connection.Open();
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add((reader.GetUInt64(0), reader.GetInt32(1)));
                }
            }

            return users;
        }
        
        public async void AddNewUser(SocketGuildUser user)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand(
                        $"INSERT INTO users SET username=@Username, userid='{user.Id}', discriminator='{user.DiscriminatorValue}'," +
                        $"avatar='{user.AvatarId}', " +
                        $"avatarURL='{user.GetAvatarUrl()}'," +
                        $"bot='{(user.IsBot ? 1 : 0)}', status=@Status, joinDate='{DateTime.Now:yyyy-MM-dd HH:mm:ss}', udc=0", connection);
                    command.Parameters.AddWithValue("@Username", user.Username);
                    command.Parameters.AddWithValue("@Status", user.Status);
                    connection.Open();
                    command.ExecuteNonQuery();
                    await _logging.LogAction($"User {user.Username}#{user.DiscriminatorValue} succesfully added to the databse.",
                        true,
                        false);
                }
            }
            catch (Exception e)
            {
                await _logging.LogAction($"Error when trying to add user {user.Id} to the database : {e}", true, false);
            }
        }

        public async void DeleteUser(ulong id)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand($"DELETE FROM users WHERE userid='{id}'", connection);
                    var command2 = new MySqlCommand($"INSERT users_remove SELECT * FROM users WHERE userid='{id}'", connection);
                    connection.Open();
                    command2.ExecuteNonQuery();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                await _logging.LogAction($"Error when trying to delete user {id} from the database : {e}", true, false);
            }
        }

        private async void UpdateAttributeFromUser(ulong id, string attribute, int value)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    
                    var command = new MySqlCommand($"UPDATE users SET {attribute}=@Value WHERE userid='{id}'", connection);
                    command.Parameters.AddWithValue("@Value", value);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                await _logging.LogAction($"Error when trying to edit attribute {attribute} from user {id} with value {value} : {e}",
                    true,
                    false);
            }
        }

        public async Task<bool> UserExists(ulong id)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand($"SELECT * FROM users where userid='{id}'", connection);
                    connection.Open();
                    return (command.ExecuteScalar() != null);
                }
            }
            catch (Exception e)
            {
                await _logging.LogAction($"Error when trying to retrieve user {id} : {e}",
                    true,
                    false);
            }

            return false;
        }

        private async void UpdateAttributeFromUser(ulong id, string attribute, uint value)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand($"UPDATE users SET {attribute}=@Value WHERE userid='{id}'", connection);
                    command.Parameters.AddWithValue("@Value", value);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                await _logging.LogAction($"Error when trying to edit attribute {attribute} from user {id} with value {value} : {e}",
                    true,
                    false);
            }
        }

        private async void UpdateAttributeFromUser(ulong id, string attribute, string value)
        {
            try
            {
                value = MySqlHelper.EscapeString(value);
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand($"UPDATE users SET {attribute}=@Value WHERE userid='{id}'", connection);
                    command.Parameters.AddWithValue("@Value", value);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                await _logging.LogAction($"Error when trying to edit attribute {attribute} from user {id} with value {value} : {e}",
                    true,
                    false);
            }
        }

        private string GetAttributeFromUser(ulong id, string attribute)
        {
            try
            {
                using (var connection = new MySqlConnection(Connection))
                {
                    var command = new MySqlCommand($"Select {attribute} FROM users WHERE userid='{id}'", connection);
                    connection.Open();
                    MySqlDataReader reader;
                    using (reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            return reader[attribute].ToString();
                        }
                    }
                }
            }
            catch (Exception e) {
                _logging?.LogAction($"Error when trying to get attribute {attribute} from user {id} : {e}", true,
                    false);
            }

            return null;
        }
    }
}