using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PQLauncher.JsonTemplates;
using SukiUI.Dialogs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PQLauncher;

public class ModManagerEntry
{
    public string ID { get; set; }
    public string Name { get; set; }
    public Uri URL { get; set; }
}

public partial class ModManager : UserControl
{
    ISukiDialogManager dialogManager;
    LauncherConfig launcherConfig;

    public event EventHandler? RefreshMods;

    public ModManager(ISukiDialogManager dlgManager, LauncherConfig launcherConfig)
    {
        InitializeComponent();
        dialogManager = dlgManager;
        this.launcherConfig = launcherConfig;

        ModGrid.ItemsSource = Settings.InstalledMods.Select(x => new ModManagerEntry { ID = x.Key, Name = launcherConfig.mods[x.Key].title, URL = x.Value });
        RemoveMod.IsEnabled = false;
        OpenDirectoryBtn.IsEnabled = false;
    }

    private void OpenDirectory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var sel = ModGrid.SelectedItem as ModManagerEntry;
        if (sel != null)
        {
            Settings.InstallationPaths.TryGetValue(sel?.ID, out string path);
            if (path != null)
            {
                Platform.OpenDirectory(path.Replace("/", "\\"));
            }
        }
    }

    private void RemoveMod_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var sel = ModGrid.SelectedItem as ModManagerEntry;
        if (sel != null)
        {
            var modID = sel?.ID;
            Settings.InstalledMods.Remove(modID);
            Settings.InstallationPaths.Remove(modID);
            launcherConfig.mods.Remove(modID);
            Settings.Save();
            ModGrid.ItemsSource = Settings.InstalledMods.Select(x => new { ID = x.Key, Name = launcherConfig.mods[x.Key].title, URL = x.Value });
            RefreshMods?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AddMod_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ManagerPanel.IsVisible = false;
        AddModPanel.IsVisible = true;
    }

    private void OnMainThread(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    private void AddModConfirm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var configUrl = new Uri(ConfigUrlBox.Text);
            Task.Run(async () => { 
                await launcherConfig.DownloadMod(configUrl);
                OnMainThread(() =>
                {
                    ModGrid.ItemsSource = Settings.InstalledMods.Select(x => new { ID = x.Key, Name = launcherConfig.mods[x.Key].title, URL = x.Value });
                    RefreshMods?.Invoke(this, EventArgs.Empty);
                });
            });
        } 
        catch (Exception ex)
        {
            return;
        }
        ManagerPanel.IsVisible = true;
        AddModPanel.IsVisible = false;
    }

    private void AddCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ManagerPanel.IsVisible = true;
        AddModPanel.IsVisible = false;
    }

    private void Done_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        dialogManager.DismissDialog();
    }

    private void DataGrid_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        var sel = ModGrid.SelectedItem as ModManagerEntry;
        if (sel != null)
        {
            var modID = sel?.ID;
            if (Settings.InstalledMods[modID] == launcherConfig.defaultmods.FirstOrDefault().Key)
            {
                RemoveMod.IsEnabled = false;
            }
            else
            {
                RemoveMod.IsEnabled = true;
            }
            OpenDirectoryBtn.IsEnabled = true;
        }
        else
        {
            RemoveMod.IsEnabled = false;
            OpenDirectoryBtn.IsEnabled = false;
        }
    }
}