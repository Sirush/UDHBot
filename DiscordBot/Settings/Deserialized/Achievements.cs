using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DiscordBot.Settings.Deserialized {
    public class Achievements {
        [JsonProperty("achievements")]
        public List<Achievement> Achievement { get; set; }
    }
    
    public class Achievement {
        public String BackgroundUrl;
        public String id;
        public String name;
        public String value;
        public String requirement;
        public String description;
        public int xp;
    }
}