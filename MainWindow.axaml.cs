using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Newtonsoft.Json;
using PQLauncher.JsonTemplates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PQLauncher
{
    public partial class MainWindow : Window
    {
        LauncherConfig launcherConfig;
        string currentMod = "";

        public MainWindow()
        {
            InitializeComponent();

            launcherConfig = new LauncherConfig(new Uri(Platform.LauncherConfigurationUrl));

            NewsBlock.Text = "Loading config...";
            Task.Run(async () =>
            {
                var loaded = await LoadConfig();
                if (loaded)
                {
                    OnMainThread(() =>
                    {
                        PopulateEntries();
                    });
                }
                else
                {
                    OnMainThread(() =>
                    {
                        NewsBlock.Text = "Failed to load config";
                    });
                }
            });
        }

        async Task<bool> LoadConfig()
        {
            Directory.CreateDirectory(Platform.ConfigurationPath);
            var downloaded = await launcherConfig.DownloadConfig();
            try
            {
                if (!downloaded)
                {
                    // Load from settings
                    if (!File.Exists(Path.Join(Platform.ConfigurationPath, "config.json")))
                    {
                        return false;
                    }
                    using (var file = File.OpenRead(Path.Join(Platform.ConfigurationPath, "config.json")))
                    {
                        launcherConfig = JsonConvert.DeserializeObject<LauncherConfig>(new StreamReader(file).ReadToEnd());
                    }
                }
                else
                {
                    // Save to settings
                    using (var writer = new StreamWriter(File.Create(Path.Join(Platform.ConfigurationPath, "config.json"))))
                    {
                        writer.Write(launcherConfig.jsonData);
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        void PopulateEntries()
        {
            var defaultMod = launcherConfig.mods.First().Value;
            ModSelector.Items.Clear();

            // Add this to the mod selector
            ModSelector.Items.Add(new ComboBoxItem() { Content = defaultMod.title, Tag = defaultMod.name });
            ModSelector.SelectedIndex = 0;

            Task.Run(FetchHTTPEntries);
        }

        private void OnMainThread(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }

        async void FetchHTTPEntries()
        {
            var mod = launcherConfig.mods[currentMod];
            if (!mod.news.ready) await mod.news.Download();
            if (!mod.changelog.ready) await mod.changelog.Download();

            OnMainThread(() =>
            {
                FormatHTML(NewsBlock, mod.news.value);
            });

            OnMainThread(() =>
            {
                FormatHTML(UpdatesBlock, mod.changelog.value);
            });
        }

        void FormatHTML(TextBlock textBlock, string htmlText)
        {
            textBlock.Text = "";
            // Parse the xml
            // This is a hacky way to parse HTML, but it works for our purposes
            // Fix <br>
            htmlText = htmlText.Replace("<br>", "<br/>");
            htmlText = htmlText.Replace("<hr>", "<hr/>");
            var xml = System.Xml.Linq.XDocument.Parse(htmlText);
            // Find the body element
            var body = xml.Descendants("body").FirstOrDefault();
            // Iterate over each element

            var inlineCollection = new InlineCollection();
            foreach (var element in body.Elements())
            {
                var value = element.Value;
                // Replace <br> with actual line breaks
                value = value.Replace("<br>", "\n");
                if (element.Name == "h1")
                {
                    inlineCollection.Add(new Run(value) { FontSize = 24 });
                    inlineCollection.Add(new LineBreak());
                }
                if (element.Name == "h2")
                {
                    inlineCollection.Add(new Run(value) { FontSize = 20 });
                    inlineCollection.Add(new LineBreak());
                }
                if (element.Name == "p")
                {
                    inlineCollection.Add(new Run(value));
                    inlineCollection.Add(new LineBreak());
                    inlineCollection.Add(new LineBreak());
                }
                if (element.Name == "hr")
                {
                    inlineCollection.Add(new LineBreak());
                }
            }

            textBlock.Inlines = inlineCollection;

        }

        private void ComboBox_SelectionChanged_1(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            if (ModSelector != null && ModSelector.SelectedItem != null)
            {
                currentMod = (string)((ComboBoxItem)ModSelector.SelectedItem).Tag;
                Task.Run(FetchHTTPEntries);
            }
        }
    }
}