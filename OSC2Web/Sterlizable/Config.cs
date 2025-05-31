using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OSC2Web.Sterlizable
{
    internal class Config
    {
        public static string UserId = "";
        public string connectionId { get; set; } = "";
        public string connectionUrl { get; set; } = "wss://control.cute.bet/ws";
        public string baseControlUrl { get; set; } = "https://control.cute.bet/osc/";
        public bool debug { get; set; } = false;
        public List<string> blacklistedItems { get; set; } = new();
        public bool removeBlacklistedItems { get; set; } = true;
        
        private static Config _instance; 
        public static Config Instance { get => GetInstance();  }
        public static Config GetInstance()
        {
            if (_instance is null)
                new Config().Load();
            return _instance;
        }
        public void Save()
        {
            File.WriteAllText("config.json", JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        internal void Load()
        {
            if (File.Exists("config.json"))
            {
                _instance = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                Console.WriteLine("Loaded Config");
                _instance!.Save();
            }
            else
            {
                Save();
                Console.WriteLine("Created New Config");
            }
        }

    }
}
