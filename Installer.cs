using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PQLauncher.JsonTemplates;
using System.IO.Compression;
using System.IO.Hashing;

namespace PQLauncher
{
    public class Installer
    {
        ModConfig m_config;
        string m_baseDirectory;
        protected string m_tempDirectory;

        int cursorx;
        int cursory;

        public Installer(ModConfig config, string baseDirectory)
        {
            m_config = config;
            m_baseDirectory = baseDirectory;
            m_tempDirectory = m_baseDirectory + "/.tmp/";
        }

        /// <summary>
        /// Install the mod. Main work function
        /// </summary>
        /// <returns>True if install succeeded</returns>
        async public Task<bool> Install()
        {
            Directory.CreateDirectory(m_baseDirectory);

            //See which packages we need to install
            ModPackage[] installList = GetInstallList();

            Console.WriteLine("Packages to be installed:");
            foreach (var package in installList)
                Console.WriteLine(package.Name);

            Directory.CreateDirectory(m_tempDirectory);

            foreach (ModPackage package in installList)
            {
                bool installed = await InstallPackage(package);
                if (!installed)
                {
                    return false;
                }

            }

            Directory.Delete(m_tempDirectory, true);

            return true;
        }

        /// <summary>
        /// Download and install files from a specific mod package.
        /// </summary>
        /// <param name="package">Package whose files to download and install</param>
        /// <returns>True if install succeeded</returns>
        async Task<bool> InstallPackage(ModPackage package)
        {
            Uri address = package.Address;

            using (WebClient client = new WebClient())
            {
                //Progress updates
                cursorx = Console.CursorLeft;
                cursory = Console.CursorTop;
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    Console.SetCursorPosition(cursorx, cursory + 1);
                    ClearCurrentConsoleLine();
                    Console.Write(String.Format("\rDownload of {0} progress: {1}% {2} / {3} ", package.Name, (e.BytesReceived * 100 / e.TotalBytesToReceive), e.BytesReceived, e.TotalBytesToReceive));
                };

                //Temp file where we download the zip
                string tempFile = m_tempDirectory + address.Segments.Last();
                await client.DownloadFileTaskAsync(address, tempFile);

                //Then load that temp file
                using (var zip = ZipFile.OpenRead(tempFile))
                {
                    for (var i = 0; i < zip.Entries.Count;i++)
                    {
                        var entry = zip.Entries.ElementAt(i);
                        string fullFile = m_baseDirectory + "/" + entry.Name;
                        fullFile = fullFile.Replace('/', '\\');
                        if (File.Exists(fullFile))
                        {
                            Console.SetCursorPosition(cursorx, cursory + 2);
                            ClearCurrentConsoleLine();
                            Console.Write(string.Format("\rComputing CRC of DIR {0} progress: {1}% {2} / {3}", m_baseDirectory, (i * 100 / zip.Entries.Count), i, zip.Entries.Count));
                            //Check if we need to extract this
                            if (entry.Crc32 == GetCRC32(fullFile) || (new string[] { "mbpprefs.cs", "lbprefs.cs", "config.cs" }.Contains(entry.Name.ToLower())))
                            {
                                continue;
                            }
                        }
                        Console.SetCursorPosition(cursorx, cursory + 3);
                        ClearCurrentConsoleLine();
                        Console.Write("\r Extracting " + entry.Name);
                        entry.ExtractToFile(m_baseDirectory, true);
                    }
                }
                
                //Clean up
                File.Delete(tempFile);
            }

            return true;
        }
        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
        /// <summary>
        /// Get the list of packages that we should download and install
        /// </summary>
        /// <returns>A string list of packages</returns>
        ModPackage[] GetInstallList()
        {
            List<ModPackage> packagesToInstall = new List<ModPackage>();

            //Check current packages

            Parallel.For(0, m_config.modPackages.Values.Count,new ParallelOptions() { MaxDegreeOfParallelism = 8 },
                (int i) => {
                        var package = m_config.modPackages.Values.ElementAt(i);

                        Console.SetCursorPosition(cursorx, cursory + 2);
                        ClearCurrentConsoleLine();
                        Console.Write("\rChecking consistency of {0} progress {1}%", package.Name, (i * 100 / m_config.modPackages.Values.Count));

                    if (FindUnmatchedFile(package))
                    {
                        packagesToInstall.Add(package);
                    }
                });

            return packagesToInstall.ToArray();
        }

        /// <summary>
        /// Find if any file in a package is not present or has changed.
        /// </summary>
        /// <param name="package">Package whose files are checked</param>
        /// <returns>True if there has been a change</returns>
        bool FindUnmatchedFile(ModPackage package)
        {
            var files = package.GetFiles();
            bool retval = false;
            Parallel.For(0, files.Count(), (int i) =>
              {
                  var file = files.ElementAt(i);

                  string fullPath = m_baseDirectory + file.path;
                  fullPath = fullPath.Replace('/', '\\');
                  //New file
                  if (!File.Exists(fullPath))
                  {
                      retval = true;
                  }
                  else
                  {
                      //Changed file
                      if (!(new string[] { "mbpprefs.cs", "lbprefs.cs", "config.cs" }.Contains(file.name.ToLower())))
                      {
                          if (GetMD5(fullPath) != file.md5)
                          {
                              retval = true;
                          }
                          
                      }
                  }
              });
            //Nothing new
            return retval;
        }

        /// <summary>
        /// Get the MD5 hash of a file (hex string)
        /// </summary>
        /// <param name="path">Path of the file</param>
        /// <returns>Hex string of the hash</returns>
        string GetMD5(string path)
        {
            //https://stackoverflow.com/a/827694
            StringBuilder builder = new StringBuilder();
            MD5 hasher = MD5.Create();
            using (FileStream stream = File.OpenRead(path))
            {
                foreach (Byte b in hasher.ComputeHash(stream))
                    builder.Append(b.ToString("x2").ToLower());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Get the CRC32 hash of a file (hex string)
        /// </summary>
        /// <param name="path">Path of the file</param>
        /// <returns>Hash</returns>
        uint GetCRC32(string path)
        {
            Crc32 hasher = new Crc32();
            using (FileStream stream = File.OpenRead(path))
            {
                hasher.Append(stream);
                return hasher.GetCurrentHashAsUInt32();
            }
        }
    }
}
