using Avalonia;
using Newtonsoft.Json.Linq;
using PQLauncher.JsonTemplates;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PQLauncher
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ModConfig))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LauncherConfig))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedPlatformSpecificConverter<string>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedPlatformSpecificConverter<JObject>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedPlatformSpecificConverter<IDictionary<String, Uri>>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedField<JObject>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedField<string>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedField<IDictionary<String, String>>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DownloadedConverter<IDictionary<String, String>>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PlatformSpecificConverter<String>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PlatformSpecificConverter<Uri>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(System.Reflection.Assembly))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ModManagerEntry))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SettingsStruct))]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            // Setup AppBuilder with extensions
            AppBuilder appBuilder = AppBuilder.Configure<App>()
                                    .WithInterFont()
                                    .LogToTrace();
            try
            {
                // Try to configure the builder with suggested platform default
                return appBuilder.UsePlatformDetect();
            }
            catch (System.NotImplementedException ex) when (ex.Message.Contains("WinUIComposition"))
            {
                // Proton (Wine) does not currently support WinUIComposition 
                // If this happens, retry again but specify to just use Skia
                // https://github.com/The-New-Platinum-Team/PQLauncher/issues/1
                return appBuilder.UseSkia();
            }
        }
    }
}
