﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DiscordBot
{
    public class BotData
    {
        public DateTime LastPublisherCheck;
        public uint LastPublisherId;
    }

    //TODO: Download all avatars to cache them

    public class UpdateService
    {
        private readonly LoggingService _logging;
        private readonly PublisherService _publisher;
        private readonly DatabaseService _database;
        private readonly CancellationToken _token;
        private BotData _botData;
        private Random _random;

        public UpdateService(LoggingService logging, PublisherService publisher, DatabaseService database)
        {
            _logging = logging;
            _publisher = publisher;
            _database = database;
            _token = new CancellationToken();
            _random = new Random();

            UpdateLoop();
        }

        private void UpdateLoop()
        {
            ReadDataFromFile();
            SaveDataToFile();
            CheckDailyPublisher();
            UpdateUserRanks();
        }

        private void ReadDataFromFile()
        {
            if (File.Exists($"{Settings.GetServerRootPath()}/botdata.json"))
            {
                string json = File.ReadAllText($"{Settings.GetServerRootPath()}/botdata.json");
                _botData = JsonConvert.DeserializeObject<BotData>(json);
            }
            else
                _botData = new BotData();
        }

        /*
        ** Save data to file every 20s
        */
        private async Task SaveDataToFile()
        {
            while (true)
            {
                var json = JsonConvert.SerializeObject(_botData);
                File.WriteAllText($"{Settings.GetServerRootPath()}/botdata.json", json);
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
                    uint count = _database.GetPublisherAdCount();
                    ulong id;
                    uint rand;
                    do
                    {
                        rand = (uint)_random.Next((int) count);
                        id = _database.GetPublisherAd(rand).userId;
                    } while (id == _botData.LastPublisherId);

                    await _publisher.PostAd(rand);
                    await _logging.LogAction("Posted new daily publisher ad.", true, false);
                    _botData.LastPublisherCheck = DateTime.Now;
                    _botData.LastPublisherId = (uint) id;
                }
                if (force)
                    return;
                await Task.Delay(TimeSpan.FromMinutes(5d), _token);
            }
        }

        private async Task UpdateUserRanks()
        {
            await Task.Delay(TimeSpan.FromSeconds(30d), _token);
            while (true)
            {
                _database.UpdateUserRanks();
                await Task.Delay(TimeSpan.FromMinutes(1d), _token);
            }
        }
    }
}