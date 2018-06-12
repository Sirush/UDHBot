﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBot.Extensions;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace DiscordBot.Services
{
    public class BotData
    {
        public DateTime LastPublisherCheck { get; set; }
        public List<ulong> LastPublisherId { get; set; }
        public DateTime LastUnityDocDatabaseUpdate { get; set; }
    }

    public class UserData
    {
        public Dictionary<ulong, DateTime> MutedUsers { get; set; }
        public Dictionary<ulong, DateTime> ThanksReminderCooldown { get; set; }
        public Dictionary<ulong, DateTime> CodeReminderCooldown { get; set; }

        public UserData()
        {
            MutedUsers = new Dictionary<ulong, DateTime>();
            ThanksReminderCooldown = new Dictionary<ulong, DateTime>();
            CodeReminderCooldown = new Dictionary<ulong, DateTime>();
        }
    }

    public class CasinoData
    {
        public int SlotMachineCashPool { get; set; }
        public int LotteryCashPool { get; set; }
    }

    public class FaqData
    {
        public string Question { get; set; }
        public string Answer { get; set; }
        public string[] Keywords { get; set; }
    }
    //TODO: Download all avatars to cache them

    public class UpdateService
    {
        readonly DiscordSocketClient _client;
        private readonly LoggingService _loggingService;
        private readonly PublisherService _publisherService;
        private readonly DatabaseService _databaseService;
        private readonly AnimeService _animeService;
        private readonly CancellationToken _token;
        private BotData _botData;
        private List<FaqData> _faqData;
        private readonly Random _random;
        private AnimeData _animeData;
        private UserData _userData;
        private CasinoData _casinoData;

        private string[][] _manualDatabase;
        private string[][] _apiDatabase;

        public UpdateService(DiscordSocketClient client, LoggingService loggingService, PublisherService publisherService,
            DatabaseService databaseService, AnimeService animeService)
        {
            _client = client;
            _loggingService = loggingService;
            _publisherService = publisherService;
            _databaseService = databaseService;
            _animeService = animeService;
            _token = new CancellationToken();
            _random = new Random();

            UpdateLoop();
        }

        private void UpdateLoop()
        {
            ReadDataFromFile();
            SaveDataToFile();
            //CheckDailyPublisher();
            UpdateUserRanks();
            UpdateAnime();
            UpdateDocDatabase();
        }

        private void ReadDataFromFile()
        {
            if (File.Exists($"{Settings.GetServerRootPath()}/botdata.json"))
            {
                var json = File.ReadAllText($"{Settings.GetServerRootPath()}/botdata.json");
                _botData = JsonConvert.DeserializeObject<BotData>(json);
            }
            else
                _botData = new BotData();

            if (File.Exists($"{Settings.GetServerRootPath()}/animedata.json"))
            {
                var json = File.ReadAllText($"{Settings.GetServerRootPath()}/animedata.json");
                _animeData = JsonConvert.DeserializeObject<AnimeData>(json);
            }
            else
                _animeData = new AnimeData();

            if (File.Exists($"{Settings.GetServerRootPath()}/userdata.json"))
            {
                var json = File.ReadAllText($"{Settings.GetServerRootPath()}/userdata.json");
                _userData = JsonConvert.DeserializeObject<UserData>(json);

                Task.Run(
                    async () =>
                    {
                        while (_client.ConnectionState != ConnectionState.Connected || _client.LoginState != LoginState.LoggedIn)
                            await Task.Delay(100, _token);
                        await Task.Delay(1000, _token);
                        //Check if there are users still muted
                        foreach (var userId in _userData.MutedUsers)
                        {
                            if (_userData.MutedUsers.HasUser(userId.Key, true))
                            {
                                var guild = _client.Guilds.First();
                                var sgu = guild.GetUser(userId.Key);
                                if (sgu == null)
                                {
                                    continue;
                                }

                                var user = (IGuildUser) sgu;

                                var mutedRole = Settings.GetMutedRole(user.Guild);
                                //Make sure they have the muted role
                                if (!user.RoleIds.Contains(mutedRole.Id))
                                {
                                    await user.AddRoleAsync(mutedRole);
                                }

                                //Setup delay to remove role when time is up.
                                await Task.Run(async () =>
                                {
                                    await _userData.MutedUsers.AwaitCooldown(user.Id);
                                    await user.RemoveRoleAsync(mutedRole);
                                }, _token);
                            }
                        }
                    }, _token);
            }
            else
            {
                _userData = new UserData();
            }

            if (File.Exists($"{Settings.GetServerRootPath()}/casinodata.json"))
            {
                var json = File.ReadAllText($"{Settings.GetServerRootPath()}/casinodata.json");
                _casinoData = JsonConvert.DeserializeObject<CasinoData>(json);
            }
            else
                _casinoData = new CasinoData();

            if (File.Exists($"{Settings.GetServerRootPath()}/FAQs.json"))
            {
                var json = File.ReadAllText($"{Settings.GetServerRootPath()}/FAQs.json");
                _faqData = JsonConvert.DeserializeObject<List<FaqData>>(json);
            }
            else
            {
                _faqData = new List<FaqData>();
            }
        }


        /*
        ** Save data to file every 20s
        */

        private async void SaveDataToFile()
        {
            while (true)
            {
                var json = JsonConvert.SerializeObject(_botData);
                File.WriteAllText($"{Settings.GetServerRootPath()}/botdata.json", json);

                json = JsonConvert.SerializeObject(_animeData);
                File.WriteAllText($"{Settings.GetServerRootPath()}/animedata.json", json);

                json = JsonConvert.SerializeObject(_userData);
                File.WriteAllText($"{Settings.GetServerRootPath()}/userdata.json", json);

                json = JsonConvert.SerializeObject(_casinoData);
                File.WriteAllText($"{Settings.GetServerRootPath()}/casinodata.json", json);
                //await _logging.LogAction("Data successfully saved to file", true, false);
                await Task.Delay(TimeSpan.FromSeconds(20d), _token);
            }
        }

        public async Task CheckDailyPublisher(bool force = false)
        {
            await Task.Delay(TimeSpan.FromSeconds(10d), _token);
            while (true)
            {
                if (_botData.LastPublisherCheck < DateTime.Now - TimeSpan.FromDays(1d) || force)
                {
                    var count = _databaseService.GetPublisherAdCount();
                    ulong id;
                    uint rand;
                    do
                    {
                        rand = (uint)_random.Next((int)count);
                        id = _databaseService.GetPublisherAd(rand).userId;
                    } while (_botData.LastPublisherId.Contains(id));

                    await _publisherService.PostAd(rand);
                    await _loggingService.LogAction("Posted new daily publisher ad.", true, false);
                    _botData.LastPublisherCheck = DateTime.Now;
                    _botData.LastPublisherId.Add(id);
                }

                if (_botData.LastPublisherId.Count > 10)
                    _botData.LastPublisherId.RemoveAt(0);

                if (force)
                    return;
                await Task.Delay(TimeSpan.FromMinutes(5d), _token);
            }
        }

        private async void UpdateUserRanks()
        {
            await Task.Delay(TimeSpan.FromSeconds(30d), _token);
            while (true)
            {
                _databaseService.UpdateUserRanks();
                await Task.Delay(TimeSpan.FromMinutes(1d), _token);
            }
        }

        private async void UpdateAnime()
        {
            await Task.Delay(TimeSpan.FromSeconds(30d), _token);
            while (true)
            {
                if (_animeData.LastDailyAnimeAiringList < DateTime.Now - TimeSpan.FromDays(1d))
                {
                    _animeService.PublishDailyAnime();
                    _animeData.LastDailyAnimeAiringList = DateTime.Now;
                }

                if (_animeData.LastWeeklyAnimeAiringList < DateTime.Now - TimeSpan.FromDays(7d))
                {
                    _animeService.PublishWeeklyAnime();
                    _animeData.LastWeeklyAnimeAiringList = DateTime.Now;
                }

                await Task.Delay(TimeSpan.FromMinutes(1d), _token);
            }
        }

        public async Task<string[][]> GetManualDatabase()
        {
            if (_manualDatabase == null)
                await LoadDocDatabase();
            return _manualDatabase;
        }

        public async Task<string[][]> GetApiDatabase()
        {
            if (_apiDatabase == null)
                await LoadDocDatabase();
            return _apiDatabase;
        }

        public List<FaqData> GetFaqData() => _faqData;

        private async Task LoadDocDatabase()
        {
            if (File.Exists($"{Settings.GetServerRootPath()}/unitymanual.json") &&
                File.Exists($"{Settings.GetServerRootPath()}/unityapi.json"))
            {
                var json = File.ReadAllText($"{Settings.GetServerRootPath()}/unitymanual.json");
                _manualDatabase = JsonConvert.DeserializeObject<string[][]>(json);
                json = File.ReadAllText($"{Settings.GetServerRootPath()}/unityapi.json");
                _apiDatabase = JsonConvert.DeserializeObject<string[][]>(json);
            }
            else
                await DownloadDocDatabase();
        }

        private async Task DownloadDocDatabase()
        {
            var htmlWeb = new HtmlWeb { CaptureRedirect = true };

            var manual = await htmlWeb.LoadFromWebAsync("https://docs.unity3d.com/Manual/docdata/index.js", _token);
            var manualInput = manual.DocumentNode.OuterHtml;

            var api = await htmlWeb.LoadFromWebAsync("https://docs.unity3d.com/ScriptReference/docdata/index.js", _token);
            var apiInput = api.DocumentNode.OuterHtml;


            _manualDatabase = ConvertJsToArray(manualInput, true);
            _apiDatabase = ConvertJsToArray(apiInput, false);

            File.WriteAllText($"{Settings.GetServerRootPath()}/unitymanual.json", JsonConvert.SerializeObject(_manualDatabase));
            File.WriteAllText($"{Settings.GetServerRootPath()}/unityapi.json", JsonConvert.SerializeObject(_apiDatabase));

            string[][] ConvertJsToArray(string data, bool isManual)
            {
                string pagesInput;
                if (isManual)
                {
                    pagesInput = data.Split("info = [")[0].Split("pages=")[1];
                    pagesInput = pagesInput.Substring(2, pagesInput.Length - 4);
                }
                else
                {
                    pagesInput = data.Split("info =")[0];
                    pagesInput = pagesInput.Substring(63, pagesInput.Length - 65);
                }


                return pagesInput.Split("],[").Select(s => s.Split(",")).Select(ps => new string[] { ps[ 0 ].Replace("\"", ""), ps[ 1 ].Replace("\"", "") }).ToArray();
            }
        }

        private async void UpdateDocDatabase()
        {
            while (true)
            {
                if (_botData.LastUnityDocDatabaseUpdate < DateTime.Now - TimeSpan.FromDays(1d))
                    await DownloadDocDatabase();

                await Task.Delay(TimeSpan.FromHours(1), _token);
            }
        }

        public UserData GetUserData() => _userData;

        public void SetUserData(UserData data)
        {
            _userData = data;
        }

        public CasinoData GetCasinoData() => _casinoData;

        public void SetCasinoData(CasinoData data)
        {
            _casinoData = data;
        }
    }
}