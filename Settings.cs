using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PQLauncher.JsonTemplates;

namespace PQLauncher
{
    internal class Settings
    {
        public static Dictionary<string, string> InstallationPaths { get; set; } = new Dictionary<string, string>();
        public static Dictionary<string, Uri> InstalledMods { get; set; } = new Dictionary<string, Uri>();
        public static Dictionary<string, string> ListingMD5 { get; set; } = new Dictionary<string, string>();

        public static void Load()
        {
            if (!File.Exists(Path.Join(Platform.ConfigurationPath, "settings.json")))
            {
                // Create default settings
                var json = JsonConvert.SerializeObject(new
                {
                    InstallationPaths = new Dictionary<string, string>(),
                    InstalledMods = new Dictionary<string, Uri>(),
                    ListingMD5 = new Dictionary<string, string>()
                });
                File.WriteAllText(Path.Join(Platform.ConfigurationPath, "settings.json"), json);
            }
            else
            {
                var json = File.ReadAllText(Path.Join(Platform.ConfigurationPath, "settings.json"));
                var settings = JsonConvert.DeserializeObject<JObject>(json);
                InstallationPaths = settings["InstallationPaths"].ToObject<Dictionary<string, string>>();
                InstalledMods = settings["InstalledMods"].ToObject<Dictionary<string, Uri>>();
                ListingMD5 = settings["ListingMD5"].ToObject<Dictionary<string, string>>();
            }
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(new
            {
                InstallationPaths,
                InstalledMods,
                ListingMD5
            });
            File.WriteAllText(Path.Join(Platform.ConfigurationPath, "settings.json"), json);
        }

        public static void AddModification(ModConfig config, Uri configUri)
        {
            if (!InstalledMods.ContainsKey(config.name))
            {
                InstalledMods.Add(config.name, configUri);
                InstallationPaths.Add(config.name, Path.Join(Platform.DefaultInstallationPath, config.gamename));
                Save();
            }
        }
    }
}
