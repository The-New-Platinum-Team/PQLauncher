using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SukiUI.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PQLauncher;

public partial class ExcludedFilesManager : UserControl
{
    private readonly ISukiDialogManager _dialogManager;
    private readonly string _modName;
    private readonly string _installPath;

    public ExcludedFilesManager(ISukiDialogManager dialogManager, string modName, string installPath)
    {
        InitializeComponent();
        _dialogManager = dialogManager;
        _modName = modName;
        _installPath = installPath;

        RefreshList();

        ExcludedFilesList.SelectionChanged += (_, _) =>
        {
            RemoveFileBtn.IsEnabled = ExcludedFilesList.SelectedItem != null;
        };
    }

    private void RefreshList()
    {
        var excluded = Settings.ExcludedFiles.ContainsKey(_modName)
            ? Settings.ExcludedFiles[_modName]
            : new List<string>();
        ExcludedFilesList.ItemsSource = excluded.ToList();
    }

    private async void AddFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        IStorageFolder? suggestedStart = null;
        if (_installPath != null && Directory.Exists(_installPath))
        {
            suggestedStart = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(_installPath));
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Image File to Exclude",
            AllowMultiple = true,
            SuggestedStartLocation = suggestedStart,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image Files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tga", "*.dds", "*.pvr", "*.webp" }
                }
            }
        });

        if (files == null || files.Count == 0) return;

        if (!Settings.ExcludedFiles.ContainsKey(_modName))
            Settings.ExcludedFiles[_modName] = new List<string>();

        foreach (var file in files)
        {
            var fullPath = file.Path.LocalPath;
            var ext = Path.GetExtension(fullPath);

            // Only allow image files
            if (!Updater.ImageExtensions.Contains(ext)) continue;

            // Compute path relative to the install directory (forward slashes to match listing format)
            string relativePath;
            if (_installPath != null && Directory.Exists(_installPath))
            {
                relativePath = Path.GetRelativePath(_installPath, fullPath).Replace('\\', '/');
            }
            else
            {
                relativePath = Path.GetFileName(fullPath);
            }

            if (!Settings.ExcludedFiles[_modName].Contains(relativePath))
                Settings.ExcludedFiles[_modName].Add(relativePath);
        }

        Settings.Save();
        RefreshList();
    }

    private void RemoveFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = ExcludedFilesList.SelectedItem as string;
        if (selected == null) return;

        if (Settings.ExcludedFiles.ContainsKey(_modName))
        {
            Settings.ExcludedFiles[_modName].Remove(selected);
            Settings.Save();
            RefreshList();
        }
    }

    private void Done_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dialogManager.DismissDialog();
    }
}
