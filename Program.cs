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
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
