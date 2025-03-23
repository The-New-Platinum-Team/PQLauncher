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
    }
}
