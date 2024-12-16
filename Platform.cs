using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PQLauncher
{
    enum PlatformValue
    {
        Windows,
        Linux,
        MacOSX,
        Unknown
    }

    internal class Platform
    {
        public static string DefaultGameConfigurationUrl = "https://marbleblast.com/pq/config/config.json";

        public static string LauncherConfigurationUrl = "https://marbleblast.com/files/launcher/config.json";

        public static PlatformValue OSPlatform
        {
            get
            {
                var rti = RuntimeInformation.RuntimeIdentifier;
                if (rti.StartsWith("win"))
                    return PlatformValue.Windows;
                else if (rti.StartsWith("linux"))
                    return PlatformValue.Linux;
                else if (rti.StartsWith("osx"))
                    return PlatformValue.MacOSX;
                else
                    return PlatformValue.Unknown;
            }
        }

        public static string ConfigurationPath
        {
            get
            {
                switch (OSPlatform)
                {
                    case PlatformValue.Windows:
                        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".mblaunchercache");

                    default:
                        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mblaunchercache");
                }
            }
        }

        public static string PlatformToString(PlatformValue val)
        {
            switch (val)
            {
                case PlatformValue.Windows:
                    return "windows";
                case PlatformValue.MacOSX:
                    return "mac";
                default:
                    return "other";
            }
        }
    }
}
