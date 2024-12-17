using System;
using System.Collections.Generic;
using System.Linq;
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

            var regKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\Classes\\platinumquest");
            regKey.SetValue(null, "platinumquest URI");
            regKey.SetValue("Content Type", "application/x-platinumquest");
            regKey.SetValue("URL Protocol", "");

            var subKey = regKey.CreateSubKey("shell\\open\\command");
            subKey.SetValue(null, $"\"{currentPath}\" \"%1\"");
            Microsoft.Win32.Registry.CurrentUser.Close();
        }
    }
}
