using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace PQLauncher
{
    internal class ProtocolHandler
    {
        [SupportedOSPlatform("windows")]
        public static void TryRegister()
        {
            var currentPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            var regKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\Classes\\stopx");
            regKey.SetValue(null, "stopx URI");
            regKey.SetValue("Content Type", "application/x-stopx");
            regKey.SetValue("URL Protocol", "");

            var subKey = regKey.CreateSubKey("shell\\open\\command");
            subKey.SetValue(null, $"\"{currentPath}\" \"%1\"");
            Microsoft.Win32.Registry.CurrentUser.Close();
        }

        [SupportedOSPlatform("windows")]
        public static void TryParseArguments(Action<string[]> launchGameCb)
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 0)
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith("stopx://"))
                    {
                        var cmd = arg.Substring("stopx://".Length);
                        if (cmd.IndexOf('/') != -1)
                        {
                            var split = cmd.Split('/');
                            split[0] = "-" + split[0];
                            // Check if PQ is already running

                            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            try
                            {
                                socket.Connect("127.0.0.1", 20248);
                                socket.Send(Encoding.UTF8.GetBytes(cmd.Replace('/', ' ')));
                                socket.Close();
                                return;
                            }
                            catch (SocketException)
                            {
                                // No server running
                                launchGameCb(split);
                                return;
                            }
                        }
                    }
                }
            }
        }

        [SupportedOSPlatform("windows")]
        public static string GetOldInstallLocation()
        {
            // Grabs the old install location from the registry

            // Search HKCU first
            var hkcuKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            // Iterate over them
            if (hkcuKey != null)
            {
                for (int i = 0; i < hkcuKey.SubKeyCount; i++)
                {
                    var subKey = hkcuKey.OpenSubKey(hkcuKey.GetSubKeyNames()[i]);
                    if (subKey.GetValue("DisplayName") != null && subKey.GetValue("DisplayName").ToString().StartsWith("STOP DeluXe"))
                    {
                        // found it
                        return subKey.GetValue("InstallLocation").ToString();
                    }
                }
            }

            // Search HKLM
            var hklmKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            // Iterate over them
            if (hklmKey != null)
            {
                for (int i = 0; i < hklmKey.SubKeyCount; i++)
                {
                    var subKey = hklmKey.OpenSubKey(hklmKey.GetSubKeyNames()[i]);
                    if (subKey.GetValue("DisplayName") != null && subKey.GetValue("DisplayName").ToString().StartsWith("STOP DeluXe"))
                    {
                        // found it
                        return subKey.GetValue("InstallLocation").ToString();
                    }
                }
            }

            // Check the 64 bit paths

            // Search HKCU first
            hkcuKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            // Iterate over them
            if (hkcuKey != null)
            {
                for (int i = 0; i < hkcuKey.SubKeyCount; i++)
                {
                    var subKey = hkcuKey.OpenSubKey(hkcuKey.GetSubKeyNames()[i]);
                    if (subKey.GetValue("DisplayName") != null && subKey.GetValue("DisplayName").ToString().StartsWith("STOP DeluXe"))
                    {
                        // found it
                        return subKey.GetValue("InstallLocation").ToString();
                    }
                }
            }

            // Search HKLM
            hklmKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            // Iterate over them
            if (hklmKey != null)
            {
                for (int i = 0; i < hklmKey.SubKeyCount; i++)
                {
                    var subKey = hklmKey.OpenSubKey(hklmKey.GetSubKeyNames()[i]);
                    if (subKey.GetValue("DisplayName") != null && subKey.GetValue("DisplayName").ToString().StartsWith("STOP DeluXe"))
                    {
                        // found it
                        return subKey.GetValue("InstallLocation").ToString();
                    }
                }
            }


            return null;
        }
    }
}
