using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PQLauncher.Properties;
using SukiUI.Controls;
using System;

namespace PQLauncher;

public partial class LicenseWindow : SukiWindow
{
    bool licensePage = false;
    public LicenseWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!licensePage)
        {
            licensePage = true;
            NextButton.Content = "Accept";
            LicenseText.Text = Properties.Resources.LICENSE;
        }
        else
        {
            Settings.LicenseAccepted = true;
            Settings.Save();
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}