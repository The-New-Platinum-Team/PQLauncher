using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PQLauncher.JsonTemplates
{
    public class ModConfig
    {
        public String name;
        public String gamename;
        public String shortname;
        public Uri image;
        [JsonConverter(typeof(DownloadedPlatformSpecificConverter<JObject>))]
        public DownloadedField<JObject> prunelist;
        [JsonConverter(typeof(DownloadedPlatformSpecificConverter<IDictionary<String, Uri>>))]
        public DownloadedField<IDictionary<String, Uri>> packages;
        [JsonConverter(typeof(DownloadedPlatformSpecificConverter<JObject>))]
        public DownloadedField<JObject> listing;
        [JsonConverter(typeof(DownloadedConverter<IDictionary<String, String>>))]
        public DownloadedField<IDictionary<String, String>> conversions;
        [JsonConverter(typeof(DownloadedConverter<IDictionary<String, String>>))]
        public DownloadedField<IDictionary<String, String>> migrations;
        [JsonConverter(typeof(DownloadedConverter<IDictionary<String, String>>))]
        public DownloadedField<IDictionary<String, String>> searches;
        public String prefsfile;
        public String lineending;
        public String opensub;
        public String rootname;
        public String title;
        [JsonConverter(typeof(DownloadedPlatformSpecificConverter<string>))]
        public DownloadedField<string> news;
        [JsonConverter(typeof(DownloadedConverter<string>))]
        public DownloadedField<string> changelog;
        public String doconsolepost;
        public Uri consolepost;
        public String consoleposttitle;
        public String consolepostmessage;
        public String consolepostattachmentname;
        public String consolepostattachmentfile;
        public String offlinetitle;
        public String offlinemessage;
        public String docopyprefs;
        public String copyprefsask;
        public String copyposttitle;
        public String copyprefsmessage;
        public IDictionary<String, String> copydata;
        [JsonConverter(typeof(PlatformSpecificConverter<String>))]
        public String launchpath;

        public IDictionary<String, ModPackage> modPackages;

        /// <summary>
        /// Download all the DownloadedField config options (task)
        /// </summary>
        /// <returns></returns>
        async public Task<bool> DownloadConfig()
        {
            bool[] results = await Task.WhenAll<bool>(
                prunelist.Download(),
                packages.Download(),
                listing.Download(),
                conversions.Download(),
                migrations.Download(),
                searches.Download()
            );

            if (results.Contains(false))
            {
                return false;
            }

            modPackages = new Dictionary<String, ModPackage>();
            return FindPackages(listing.value, "");
        }

        bool FindPackages(JObject root, string path = "")
        {
            foreach (JToken token in root.Properties())
            {
                if (token is JProperty)
                {
                    JProperty prop = token as JProperty;
                    if (prop.Value.Type == JTokenType.String && prop.Name == "md5")
                    {
                        //It's a file
                        string md5 = prop.Value.ToString();
                        string package = root["package"].ToString();

                        if (!modPackages.ContainsKey(package))
                        {
                            AddModPackage(package);
                        }
                        if (!modPackages[package].AddFile(path, md5))
                        {
                            return false;
                        }
                    }
                    else if (prop.Value is JObject)
                    {
                        FindPackages(prop.Value as JObject, path + "/" + prop.Name);
                    }
                }
            }
            return true;
        }

        void AddModPackage(string name)
        {
            //Find the address
            Uri address = packages.value[name + ".zip"];
            //And add the package
            ModPackage package = new ModPackage(name, address);
            modPackages.Add(name, package);
        }

        async public Task<bool> InstallMod(string installPath)
        {
            //Need install info before we can go installing
            if (!await DownloadConfig())
            {
                return false;
            }

            Installer installer = new Installer(this, installPath);
            return await installer.Install();
        }
    }
}
