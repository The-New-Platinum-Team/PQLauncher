using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Newtonsoft.Json;
using PQLauncher.JsonTemplates;
using SukiUI.Controls;
using SukiUI.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PQLauncher
{
    public partial class MainWindow : SukiWindow
    {
        LauncherConfig launcherConfig;
        string currentMod = "";
        Updater updater;
        System.Diagnostics.Process gameProcess;

        public static ISukiDialogManager DialogManager = new SukiDialogManager();

        public MainWindow()
        {
            InitializeComponent();

            NewsBusy.IsBusy = true;
            UpdatesBusy.IsBusy = true;
            SettingsBusy.IsBusy = true;

            DownloadProgress.IsVisible = false;
            UpdateProgress.IsVisible = false;
            PlayButton.IsVisible = true;
            PlayButton.IsEnabled = false;

            DialogHost.Manager = DialogManager;

            Settings.Load();

            if (Platform.OSPlatform == PlatformValue.Windows)
            {
                ProtocolHandler.TryRegister();
            }

            launcherConfig = new LauncherConfig(new Uri(Platform.LauncherConfigurationUrl));

            Task.Run(async () =>
            {
                var loaded = await LoadConfig();
                if (loaded)
                {
                    OnMainThread(() =>
                    {
                        PopulateEntries();
                        PlayButton.IsEnabled = true;
                    });
                }
                else
                {
                    OnMainThread(() =>
                    {
                        NewsBusy.IsBusy = false;
                        UpdatesBusy.IsBusy = false;
                        SettingsBusy.IsBusy = false;
                        PlayButton.IsEnabled = true;
                        NewsBlock.Text = "Failed to load config";
                        UpdatesBlock.Text = "Failed to load config";
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

            // Add the rest of the mods
            foreach (var mod in launcherConfig.mods)
            {
                if (mod.Key != defaultMod.name)
                {
                    ModSelector.Items.Add(new ComboBoxItem() { Content = mod.Value.title, Tag = mod.Key });
                }
            }

            // Configure mods..
            ModSelector.Items.Add(new ComboBoxItem() { Content = "Configure Mods...", Tag = "configure" });

            SettingsBusy.IsBusy = false;

            Task.Run(FetchHTTPEntries);
        }

        private void OnMainThread(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }

        async void FetchHTTPEntries()
        {
            OnMainThread(() =>
            {
                NewsBusy.IsBusy = true;
                UpdatesBusy.IsBusy = true;
            });
            var mod = launcherConfig.mods[currentMod];
            if (!mod.news.ready) await mod.news.Download();
            if (!mod.changelog.ready) await mod.changelog.Download();

            OnMainThread(() =>
            {
                FormatHTML(NewsBlock, mod.news.value);
                NewsBusy.IsBusy = false;
            });

            OnMainThread(() =>
            {
                FormatHTML(UpdatesBlock, mod.changelog.value);
                UpdatesBusy.IsBusy = false;
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
                if ((string)((ComboBoxItem)ModSelector.SelectedItem).Tag == "configure")
                {
                    ModSelector.SelectedItem = ModSelector.Items.Where(x => ((string)((ComboBoxItem)x).Tag) == currentMod).FirstOrDefault();

                    var modManager = new ModManager(DialogManager, launcherConfig);
                    modManager.RefreshMods += (object sender, EventArgs e) =>
                    {
                        PopulateEntries();
                    };

                    var dlg = DialogManager.CreateDialog();
                    dlg.SetContent(modManager);
                    dlg.TryShow();
                }
                else
                {
                    currentMod = (string)((ComboBoxItem)ModSelector.SelectedItem).Tag;
                    Task.Run(FetchHTTPEntries);
                }
            }
        }

        private void OpenGame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (currentMod != null && currentMod != "")
            {
                Settings.InstallationPaths.TryGetValue(currentMod, out string path);
                if (path != null)
                {
                    Platform.OpenDirectory(path.Replace("/", "\\"));
                }
            }
        }

        private void ChangeGameLocation_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (currentMod != null && currentMod != "") { 
                Settings.InstallationPaths.TryGetValue(currentMod, out string path);
                if (path != null)
                {
                    Settings.InstallationPaths.TryGetValue(currentMod, out string existingPath);
                    Task.Run(async () =>
                    {
                        var folder = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions()
                        {
                            AllowMultiple = false,
                            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(existingPath)),
                            Title = "Select the game folder",
                        });
                        if (folder.Count != 0)
                        {
                            // Set the game folder
                            var selectedFolder = folder.First().Path.ToString();
                            selectedFolder = selectedFolder.Replace("file:///", "");
                            Settings.InstallationPaths[currentMod] = selectedFolder;
                            Settings.Save();
                        }
                    });
                }
            }
        }

        void DoPlayButton(bool fullUpdate)
        {
            if (currentMod != null && currentMod != "")
            {
                PlayButton.IsVisible = false;
                PlayButton.IsEnabled = false;
                DownloadProgress.IsVisible = true;
                UpdateProgress.IsVisible = true;
                DownloadProgress.Value = 0.0;
                UpdateProgress.Value = 0.0;

                updater = new Updater(launcherConfig.mods[currentMod]);
                updater.ProgressUpdate += (object sender, UpdateProgress progress) =>
                {
                    OnMainThread(() =>
                    {
                        DownloadProgress.Value = progress.progress * 100.0 / progress.total;
                    });
                };
                updater.DownloadProgressUpdate += (object sender, UpdateProgress progress) =>
                {
                    OnMainThread(() =>
                    {
                        UpdateProgress.Value = progress.progress * 100.0 / progress.total;
                    });
                };
                updater.Logger += (object sender, string message) =>
                {
                    OnMainThread(() =>
                    {
                        ConsoleBlock.Text += message + "\n";
                        ConsoleScroll.ScrollToEnd();
                    });
                };
                Task.Run(async () => {
                    var res = await updater.Update(fullUpdate);

                    OnMainThread(() =>
                    {
                        if (!res)
                        {
                            var dlg = DialogManager.CreateDialog();
                            dlg.SetTitle("Error");
                            dlg.SetType(Avalonia.Controls.Notifications.NotificationType.Error);
                            dlg.SetCanDismissWithBackgroundClick(true);
                            dlg.SetContent("Failed to update the game. Launch the game anyway?");
                            dlg.AddActionButton("Launch", _ => { LaunchGame(true); }, true);
                            dlg.AddActionButton("Close", _ => { }, true);

                            dlg.TryShow();
                        }
                        else
                        {
                            LaunchGame(false);
                        }

                        DownloadProgress.IsVisible = false;
                        UpdateProgress.IsVisible = false;

                        PlayButton.IsVisible = true;
                        PlayButton.IsEnabled = true;
                    });
                });
            }
        }

        private void Play_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            DoPlayButton(false);
        }

        void LaunchGame(bool offline)
        {
            if (currentMod != null && currentMod != "")
            {
                PlayButton.IsEnabled = false;

                var gamePath = Settings.InstallationPaths[currentMod];
                var executablePath = launcherConfig.mods[currentMod].launchpath.TrimStart('/');

                var fullPath = Path.Join(gamePath, executablePath);
                gameProcess = Platform.LaunchGame(fullPath, offline);
                if (gameProcess != null)
                {
                    gameProcess.EnableRaisingEvents = true;
                    gameProcess.Exited += (object sender, EventArgs e) =>
                    {
                        OnMainThread(() =>
                        {
                            PlayButton.IsEnabled = true;
                            IsVisible = true;
                        });
                    };
                    IsVisible = false;
                }
            }
        }

        private void FullUpdate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            DoPlayButton(true);
        }

        private void Import_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (currentMod != null && currentMod != "")
            {
                Task.Run(async () =>
                {
                    var file = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
                    {
                        AllowMultiple = false,
                        SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(Settings.InstallationPaths[currentMod])),
                        Title = "Select the preferences file",
                    });
                    if (file.Count != 0)
                    {
                        using (var fileStream = await file.First().OpenReadAsync())
                        {
                            using (var sr = new StreamReader(fileStream))
                            {
                                var prefsData = await sr.ReadToEndAsync();
                                // This needs to be appended to the existing prefs file

                                var prefsPath = Path.Join(Settings.InstallationPaths[currentMod], launcherConfig.mods[currentMod].prefsfile.TrimStart('/'));
                                var prefsDir = Path.GetDirectoryName(prefsPath);
                                if (prefsDir != null)
                                {
                                    // Check if we can find the prefs file - case insensitive
                                    var prefsFile = Directory.GetFiles(prefsDir, Path.GetFileName(prefsPath), new EnumerationOptions() { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false }).FirstOrDefault();

                                    // Append
                                    if (prefsFile != null)
                                    {
                                        using (var writer = File.AppendText(prefsFile))
                                        {
                                            await writer.WriteLineAsync();
                                            await writer.WriteAsync(prefsData);
                                        }
                                    }
                                    else
                                    {
                                        // Write new
                                        using (var writer = File.CreateText(prefsPath))
                                        {
                                            await writer.WriteAsync(prefsData);
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}