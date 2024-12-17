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

        public static string DefaultInstallationPath
        {
            get
            {
                switch (OSPlatform)
                {
                    case PlatformValue.Windows:
                        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    case PlatformValue.MacOSX:
                        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications");

                    default:
                        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
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

        public static void OpenDirectory(string path)
        {
            switch (OSPlatform)
            {
                case PlatformValue.Windows:
                    System.Diagnostics.Process.Start("explorer.exe", path);
                    break;

                case PlatformValue.MacOSX:
                    System.Diagnostics.Process.Start("open", ["-R", path]);
                    break;

                case PlatformValue.Linux:
                    System.Diagnostics.Process.Start("xdg-open", path);
                    break;
            }
        }

        public static System.Diagnostics.Process LaunchGame(string path, bool offline)
        {
            switch (OSPlatform)
            {
                case PlatformValue.Windows:
                case PlatformValue.Linux:
                case PlatformValue.Unknown:
                    {
                        if (OSPlatform != PlatformValue.Windows)
                        {
                            // Check if we can execute it
                            var attribs = File.GetUnixFileMode(path);
                            if ((attribs & UnixFileMode.UserExecute) == 0)
                                attribs |= UnixFileMode.UserExecute;
                            if ((attribs & UnixFileMode.GroupExecute) == 0)
                                attribs |= UnixFileMode.GroupExecute;
                            if ((attribs & UnixFileMode.OtherExecute) == 0)
                                attribs |= UnixFileMode.OtherExecute;
                            try
                            {
                                File.SetUnixFileMode(path, attribs);
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                        var psi = new System.Diagnostics.ProcessStartInfo(path);
                        if (offline)
                            psi.Arguments = "-offline";
                        psi.WorkingDirectory = Path.GetDirectoryName(path);
                        return System.Diagnostics.Process.Start(psi);
                    }

                case PlatformValue.MacOSX:
                    {
                        // Check if we can execute it
                        var attribs = File.GetUnixFileMode(path);
                        if ((attribs & UnixFileMode.UserExecute) == 0)
                            attribs |= UnixFileMode.UserExecute;
                        if ((attribs & UnixFileMode.GroupExecute) == 0)
                            attribs |= UnixFileMode.GroupExecute;
                        if ((attribs & UnixFileMode.OtherExecute) == 0)
                            attribs |= UnixFileMode.OtherExecute;
                        try
                        {
                            File.SetUnixFileMode(path, attribs);
                        }
                        catch (Exception ex)
                        {
                        }

                        var psi = new System.Diagnostics.ProcessStartInfo(path);
                        if (offline)
                            psi.Arguments = "-nohomedir -offline";
                        else
                            psi.Arguments = "-nohomedir";
                        psi.WorkingDirectory = Path.GetDirectoryName(path);
                        return System.Diagnostics.Process.Start(psi);
                    }

                default:
                    return null;
            }
        }
    }
}
