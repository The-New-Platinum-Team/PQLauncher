using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Net.Http;

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

        public static string LauncherConfigurationUrl = "https://marbleblast.com/files/launcher/config-new.json";

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

                    case PlatformValue.MacOSX:
                        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mblaunchercache");

                    default:
                        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".walaunchercache");
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
                        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
                    System.Diagnostics.Process.Start("explorer.exe", path.Replace("/", "\\"));
                    break;

                case PlatformValue.MacOSX:
                    System.Diagnostics.Process.Start("open", ["-R", path]);
                    break;

                case PlatformValue.Linux:
                    System.Diagnostics.Process.Start("xdg-open", path);
                    break;
            }
        }

        public static System.Diagnostics.Process LaunchGame(string path, bool offline, string[] args)
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

                        if (args != null)
                            psi.Arguments += string.Join(' ', args);

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

                        if (args != null)
                            psi.Arguments += string.Join(' ', args);

                        psi.WorkingDirectory = Path.GetDirectoryName(path);
                        return System.Diagnostics.Process.Start(psi);
                    }

                default:
                    return null;
            }
        }


        // VCRedist download URLs (update these to the version you need)
        private const string VCREDIST_URL = "https://aka.ms/vc14/vc_redist.x64.exe";

        /// <summary>
        /// Checks if Visual C++ Redistributable is installed
        /// </summary>
        /// <param name="architecture">Architecture: "x64" or "x86"</param>
        /// <param name="minVersion">Minimum version required (optional)</param>
        /// <returns>True if installed, false otherwise</returns>
        public static bool IsVCRedistInstalled(EventHandler<string>? logger)
        {
            try
            {
                string registryPath =  @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64";

                // Check in both 64-bit and 32-bit registry views
                var registryViews = new[] { RegistryView.Registry64, RegistryView.Registry32 };

                foreach (var view in registryViews)
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var key = baseKey.OpenSubKey(registryPath))
                    {
                        if (key != null)
                        {
                            var installed = key.GetValue("Installed");
                            if (installed != null && (int)installed == 1)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger?.Invoke(null, $"Error checking VCRedist installation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads VCRedist installer to a temporary location
        /// </summary>
        /// <param name="architecture">Architecture: "x64" or "x86"</param>
        /// <returns>Path to downloaded installer</returns>
        public static async Task<string> DownloadVCRedist(EventHandler<string>? logger)
        {
            string url = VCREDIST_URL;
            string tempPath = Path.Combine(Path.GetTempPath(), $"vc_redist_x86.exe");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5);

                    var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var bytesRead = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int read;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            bytesRead += read;

                            if (totalBytes > 0)
                            {
                                var progress = (int)((bytesRead * 100) / totalBytes);
                            }
                        }
                    }

                    logger?.Invoke(null, "\nDownload completed!");
                    return tempPath;
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke(null, $"\nError downloading VCRedist: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Installs VCRedist from the specified installer path
        /// </summary>
        /// <param name="installerPath">Path to the installer exe</param>
        /// <param name="silent">Whether to install silently without UI</param>
        /// <returns>True if installation succeeded</returns>
        public static bool InstallVCRedist(string installerPath, EventHandler<string>? logger, bool silent = true)
        {
            try
            {
                if (!File.Exists(installerPath))
                {
                    logger?.Invoke(null, $"Installer not found at: {installerPath}");
                    return false;
                }

                logger?.Invoke(null, "Installing VCRedist...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = silent ? "/install /quiet /norestart" : "/install /passive /norestart",
                    UseShellExecute = true,
                    Verb = "runas" // Request administrator privileges
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        logger?.Invoke(null, "Failed to start installer process.");
                        return false;
                    }

                    process.WaitForExit();

                    // Exit code 0 or 3010 (reboot required) indicate success
                    if (process.ExitCode == 0 || process.ExitCode == 3010)
                    {
                        logger?.Invoke(null, "Installation completed successfully!");
                        if (process.ExitCode == 3010)
                        {
                            logger?.Invoke(null, "Note: A system reboot may be required.");
                        }
                        return true;
                    }
                    else if (process.ExitCode == 1638)
                    {
                        logger?.Invoke(null, "VCRedist is already installed (newer version).");
                        return true;
                    }
                    else
                    {
                        logger?.Invoke(null, $"Installation failed with exit code: {process.ExitCode}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke(null, $"Error installing VCRedist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks, downloads, and installs VCRedist if needed
        /// </summary>
        public static async Task<bool> EnsureVCRedistInstalled(EventHandler<string>? logger)
        {
            logger?.Invoke(null, $"Checking for VCRedist...");

            if (IsVCRedistInstalled(logger))
            {
                logger?.Invoke(null, "VCRedist is already installed.");
                return true;
            }

            logger?.Invoke(null, "VCRedist not found. Installing...");

            string installerPath = null;
            try
            {
                installerPath = await DownloadVCRedist(logger);
                bool success = InstallVCRedist(installerPath, logger);
                return success;
            }
            finally
            {
                // Clean up downloaded installer
                if (!string.IsNullOrEmpty(installerPath) && File.Exists(installerPath))
                {
                    try
                    {
                        File.Delete(installerPath);
                        logger?.Invoke(null, "Cleaned up temporary installer.");
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}
