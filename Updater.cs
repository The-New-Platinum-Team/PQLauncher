using Newtonsoft.Json.Linq;
using PQLauncher.JsonTemplates;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PQLauncher
{
    internal struct UpdateProgress
    {
        public long progress = 0;
        public long total = 0;
        public UpdateProgress(long p, long t) { progress = p; total = t; }
    }

    internal record ListingEntry(string FileName, string ActualPath, string Path, string MD5, string package);

    internal class Updater
    {
        // Event for progress updates
        public event EventHandler<UpdateProgress>? DownloadProgressUpdate;
        public event EventHandler<UpdateProgress>? ProgressUpdate;
        public event EventHandler<string>? Logger;

        Dictionary<string, List<ListingEntry>> filesToUpdate = new(); // Package => List of files

        ModConfig game;

        public Updater(ModConfig game)
        {
            this.game = game;
        }

        public async Task<bool> Update()
        {
            var http = new HttpClient();

            /*
			 * Step 1: Download the package listing
			 */
            ProgressUpdate?.Invoke(this, new UpdateProgress(0, 5));

            if (!game.packages.ready)
            {
                Logger?.Invoke(this, $"Downloading package listing for mod {game.name}");
            }

            if (!game.packages.ready && !await game.packages.DownloadWithProgress((a, b) => DownloadProgressUpdate?.Invoke(this, new UpdateProgress(a, b))))
            {
                Logger?.Invoke(this, $"Could not download package listing for mod {game.name}. The mod will not be able to be updated.");
                return false;
            }

            ProgressUpdate?.Invoke(this, new UpdateProgress(1, 5));

            foreach (var kvp in game.packages.value)
            {
                Logger?.Invoke(this, $"Got package name: {kvp.Key}, address is {kvp.Value.ToString()}");
            }

            // Do pruning
            if (!game.prunelist.ready && !await game.prunelist.DownloadWithProgress((a, b) => DownloadProgressUpdate?.Invoke(this, new UpdateProgress(a, b))))
            {
                Logger?.Invoke(this, $"Could not download prune list for mod {game.name}. The mod will not be able to be updated.");
                return false;
            }

            PruneIterate(game.prunelist.value, Settings.InstallationPaths[game.name]);

            ProgressUpdate?.Invoke(this, new UpdateProgress(2, 5));

            var cachedListingMD5 = Settings.ListingMD5.ContainsKey(game.name) ? Settings.ListingMD5[game.name] : "";

            var listingPath = Path.Join(Platform.ConfigurationPath, "mod", game.listing.m_address.ToString().Replace("/", "_").Replace(":", ""), "listing.json");
            // Get the listing
            if (!game.listing.ready)
            {
                Logger?.Invoke(this, $"Downloading listing for mod {game.name}");
                var res = await game.listing.DownloadWithProgress((a, b) => DownloadProgressUpdate?.Invoke(this, new UpdateProgress(a, b)));
                if (!res)
                {
                    // Check for cached listing
                    if (File.Exists(listingPath))
                    {
                        game.listing.value = JObject.Parse(File.ReadAllText(listingPath));
                        game.listing.ready = true;
                    }
                    else
                    {
                        Logger?.Invoke(this, $"Could not download listing for mod {game.name}. The mod will not be able to be updated.");
                        return false;
                    }
                }
                else
                {
                    // Save the listing cache
                    Directory.CreateDirectory(Path.GetDirectoryName(listingPath));
                    File.WriteAllText(listingPath, game.listing.value.ToString());
                }
            }

            ProgressUpdate?.Invoke(this, new UpdateProgress(3, 5));

            var installExists = Directory.Exists(Settings.InstallationPaths[game.name]);

            if (installExists)
            {
                // Backup our prefs first!
                var prefsPath = Path.Join(Settings.InstallationPaths[game.name], game.prefsfile.TrimStart('/'));
                var prefsDir = Path.GetDirectoryName(prefsPath);
                // Check if we can find the prefs file - case insensitive
                var prefsFile = Directory.GetFiles(prefsDir, Path.GetFileName(prefsPath), new EnumerationOptions() { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false }).FirstOrDefault();
                if (prefsFile != null)
                {
                    Logger?.Invoke(this, $"Backing up preferences file {prefsFile}.");
                    File.Copy(prefsFile, prefsFile + ".bak", true);
                }

                if (!Settings.ListingMD5.ContainsKey(game.name) || cachedListingMD5 != Settings.ListingMD5[game.name])
                {
                    Logger?.Invoke(this, "Listing doesn't match, updating files.");

                    var fileCount = CountFiles(game.listing.value);
                    CheckConsistency(game.listing.value, fileCount);
                }
                else
                {
                    Logger?.Invoke(this, "Listing matches, no need to update.");
                }
            }
            else
            {
                Logger?.Invoke(this, "No game found; doing full update.");
            }

            ProgressUpdate?.Invoke(this, new UpdateProgress(4, 5));

            await UpdateFiles();

            ProgressUpdate?.Invoke(this, new UpdateProgress(5, 5));
            // Get the listing MD5
            Settings.ListingMD5[game.name] = GetMD5(listingPath);

            return true;
        }

        void PruneIterate(JObject obj, string path)
        {
            foreach (var key in obj)
            {
                if (key.Value.Type == JTokenType.Object)
                {
                    PruneIterate(key.Value as JObject, Path.Join(path, key.Key));
                }
                else if (key.Value.Type == JTokenType.String)
                {
                    string file = Path.Join(path, key.Key);
                    if (File.Exists(file))
                    {
                        Logger?.Invoke(this, $"Deleting {file}");
                        File.Delete(file);
                    }
                }
            }
        }

        int CountFiles(JObject obj)
        {
            int count = 0;
            foreach (var key in obj)
            {
                if (key.Value.Type == JTokenType.Object)
                {
                    var jo = key.Value as JObject;
                    if (jo.GetValue("md5") != null && jo.GetValue("package") != null)
                        count++;
                    else
                        count += CountFiles(key.Value as JObject);
                }
            }
            return count;
        }

        void CheckConsistency(JObject listing, int count)
        {
            var done = 0;
            var installPath = Settings.InstallationPaths[game.name];
            var res = Parallel.ForEach(IterateFiles(listing, installPath, ""), (a, _) =>
            {
                done += 1;
                ProgressUpdate?.Invoke(this, new UpdateProgress(done, count));
                var ourMD5 = GetMD5(a.Path);
                if (ourMD5 != a.MD5)
                {
                    Logger?.Invoke(this, $"Discrepancy in file: {a.FileName} ({ourMD5} != {a.MD5})");
                    if (filesToUpdate.ContainsKey(a.package))
                    {
                        filesToUpdate[a.package].Add(a);
                    }
                    else
                    {
                        filesToUpdate[a.package] = new List<ListingEntry> { a };
                    }
                }
            });
        }

        async Task UpdateFiles()
        {
            foreach (var kvp in filesToUpdate)
            {
                Logger?.Invoke(this, $"Getting patch {kvp.Key}.");

                var packageBytes = await DownloadWithProgress(game.packages.value[kvp.Key], (a, b) => DownloadProgressUpdate?.Invoke(this, new UpdateProgress(a, b)));
                if (packageBytes != null)
                {
                    using (var zipStream = new MemoryStream(packageBytes))
                    {
                        using (var archive = new ZipArchive(zipStream))
                        {
                            foreach (var entry in kvp.Value)
                            {
                                var file = archive.GetEntry(entry.ActualPath.Replace("\\", "/"));
                                if (file != null)
                                {
                                    Logger?.Invoke(this, $"Updating file {entry.FileName}.");
                                    using (var fileStream = file.Open())
                                    {
                                        using (var fs = File.OpenWrite(entry.Path))
                                        {
                                            await fileStream.CopyToAsync(fs);
                                        }
                                    }
                                }
                                else
                                {
                                    Logger?.Invoke(this, $"Failed to find file {entry.FileName} in package {kvp.Key}.");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger?.Invoke(this, $"Failed to download package {kvp.Key}.");
                }
            }
        }

        IEnumerable<ListingEntry> IterateFiles(JObject obj, string path, string actualPath)
        {
            foreach (var key in obj)
            {
                if (key.Value.Type == JTokenType.Object)
                {
                    var jo = key.Value as JObject;
                    if (jo.GetValue("md5") != null && jo.GetValue("package") != null)
                    {
                        var md5 = jo.GetValue("md5").ToString();
                        var package = jo.GetValue("package").ToString();
                        yield return new ListingEntry(key.Key, Path.Join(actualPath, key.Key), Path.Join(path, key.Key), md5, package + ".zip");
                    }
                    else
                    {
                        foreach (var x in IterateFiles(key.Value as JObject, Path.Join(path, key.Key), Path.Join(actualPath, key.Key)))
                        {
                            yield return x;
                        }
                    }
                }
            }
            yield break;
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
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    foreach (Byte b in hasher.ComputeHash(stream))
                        builder.Append(b.ToString("x2").ToLower());
                }
            }
            catch (FileNotFoundException ex)
            {
                return "";
            }

            return builder.ToString();
        }

        async public Task<byte[]> DownloadWithProgress(Uri address, Action<long, long> progresser)
        {
            var client = new HttpClient();

            try
            {
                // Must use ResponseHeadersRead to avoid buffering of the content
                using (var response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead))
                {
                    // You must use as stream to have control over buffering and number of bytes read/received
                    var chunks = new List<byte[]>();
                    long readBytes = 0;
                    long? totalBytes = response.Content.Headers.ContentLength;
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        // Read/process bytes from stream as appropriate

                        while (true)
                        {
                            var chunk = new byte[8192];
                            var bytes = await stream.ReadAsync(chunk, 0, chunk.Length);
                            if (bytes == 0)
                            {
                                break;
                            }
                            readBytes += bytes;
                            chunks.Add(chunk[0..bytes]);
                            progresser(readBytes, (long)totalBytes);
                        }
                    }
                    if (readBytes != totalBytes)
                    {
                        return null;
                    }
                    var output = new byte[chunks.Sum(arr => arr.Length)];
                    int writeIdx = 0;
                    foreach (var byteArr in chunks)
                    {
                        byteArr.CopyTo(output, writeIdx);
                        writeIdx += byteArr.Length;
                    }
                    return output;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
