using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace PQLauncher
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Settings.Load();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (Settings.LicenseAccepted)
                {
                    desktop.MainWindow = new MainWindow();
                } 
                else
                {
                    desktop.MainWindow = new LicenseWindow();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}