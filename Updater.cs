using Newtonsoft.Json.Linq;
using PQLauncher.JsonTemplates;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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

        public async Task<bool> Update(bool ignoreCache)
        {
            var http = new HttpClient()
            {
                DefaultRequestVersion = HttpVersion.Version20,
            };

            /*
             * Step 0: Ensure VCRedist if we are on windows
             */
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await Platform.EnsureVCRedistInstalled(Logger);
            }

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
                try {
                    var prefsFile = Directory.GetFiles(prefsDir, Path.GetFileName(prefsPath), new EnumerationOptions() { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false }).FirstOrDefault();
                    if (prefsFile != null)
                    {
                        Logger?.Invoke(this, $"Backing up preferences file {prefsFile}.");
                        File.Copy(prefsFile, prefsFile + ".bak", true);
                    }
                }
                catch (Exception e)
                {
                     Logger?.Invoke(this, $"Failed to backup preferences file.");
                }

                if (!Settings.ListingMD5.ContainsKey(game.name) || cachedListingMD5 != Settings.ListingMD5[game.name] || ignoreCache)
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
                // Ensure installation directory
                Directory.CreateDirectory(Settings.InstallationPaths[game.name]);
                InstallNew(game.listing.value);
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
                DownloadProgressUpdate?.Invoke(this, new UpdateProgress(done, count));
                ProgressUpdate?.Invoke(this, new UpdateProgress(3 * count + done, 5 * count));
                var ourMD5 = GetMD5(a.Path);
                if (ourMD5 != a.MD5)
                {
                    if (ourMD5 != "")
                        Logger?.Invoke(this, $"Discrepancy in file: {a.FileName} ({ourMD5} != {a.MD5})");
                    lock (filesToUpdate)
                    {
                        if (filesToUpdate.ContainsKey(a.package))
                            filesToUpdate[a.package].Add(a);
                        else
                            filesToUpdate[a.package] = new List<ListingEntry> { a };
                    }
                }
            });
        }

       void InstallNew(JObject listing)
        {
            var installPath = Settings.InstallationPaths[game.name];
            var res = Parallel.ForEach(IterateFiles(listing, installPath, ""), (a, _) =>
            {
                lock (filesToUpdate)
                {
                    if (filesToUpdate.ContainsKey(a.package))
                        filesToUpdate[a.package].Add(a);
                    else
                        filesToUpdate[a.package] = new List<ListingEntry> { a };
                }
            });
        }

        async Task UpdateFiles()
        {
            var i = 0;
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
                                    if (kvp.Value.Count < 100)
                                        Logger?.Invoke(this, $"Updating file {entry.FileName}.");
                                    using (var fileStream = file.Open())
                                    {
                                        // Ensure directory
                                        Directory.CreateDirectory(Path.GetDirectoryName(entry.Path));
                                        using (var fs = File.Open(entry.Path, FileMode.Create))
                                        {
                                            fileStream.CopyTo(fs);
                                        }
                                        // Ensure MD5 matches
                                        var ourMD5 = GetMD5(entry.Path);
                                        var theirMD5 = entry.MD5;
                                        System.Diagnostics.Debug.Assert(ourMD5 == theirMD5);
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
                i++;

                ProgressUpdate?.Invoke(this, new UpdateProgress(4 * filesToUpdate.Count + i, 5 * filesToUpdate.Count));
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
                        yield return new ListingEntry(key.Key, Path.Join(actualPath, key.Key).Replace('\\', '/'), Path.Join(path, key.Key).Replace('\\', '/'), md5, package + ".zip");
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
        public static string GetMD5(string path)
        {
            if (!File.Exists(path))
                return "";
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
            catch
            {
                return "";
            }

            return builder.ToString();
        }

        private static readonly HttpClient _sharedClient = new HttpClient()
        {
            DefaultRequestVersion = HttpVersion.Version11,
        };

        async public Task<byte[]> DownloadWithProgress(Uri address, Action<long, long> progresser)
        {
            const int chunkSize = 4 * 1024 * 1024; // 4MB chunks
            const int parallelChunks = 8;

            try
            {
                // HEAD request to get file size and check range support
                using var head = await _sharedClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, address));
                head.EnsureSuccessStatusCode();

                long? totalBytes = head.Content.Headers.ContentLength;
                bool acceptsRanges = head.Headers.AcceptRanges.Contains("bytes");

                if (!totalBytes.HasValue || !acceptsRanges)
                {
                    // Fall back to single download
                    return await DownloadSingle(address, totalBytes, progresser);
                }

                long total = totalBytes.Value;
                var result = new byte[total];
                long readBytes = 0;

                // Split into chunks
                var chunks = new List<(long start, long end)>();
                for (long offset = 0; offset < total; offset += chunkSize)
                    chunks.Add((offset, Math.Min(offset + chunkSize - 1, total - 1)));

                await Parallel.ForEachAsync(chunks,
                    new ParallelOptions { MaxDegreeOfParallelism = parallelChunks },
                    async (chunk, ct) =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, address);
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(chunk.start, chunk.end);

                        using var response = await _sharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                        response.EnsureSuccessStatusCode();

                        using var stream = await response.Content.ReadAsStreamAsync(ct);
                        var buffer = new byte[256 * 1024];
                        long pos = chunk.start;
                        int bytes;
                        while ((bytes = await stream.ReadAsync(buffer, ct)) > 0)
                        {
                            Buffer.BlockCopy(buffer, 0, result, (int)pos, bytes);
                            pos += bytes;
                            Interlocked.Add(ref readBytes, bytes);
                            progresser(readBytes, total);
                        }
                    });

                return readBytes == total ? result : null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<byte[]> DownloadSingle(Uri address, long? totalBytes, Action<long, long> progresser)
        {
            const int bufSize = 1024 * 1024;
            var buffer = new byte[bufSize];

            using var response = await _sharedClient.GetAsync(address, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long readBytes = 0;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var output = new MemoryStream(totalBytes.HasValue ? (int)totalBytes.Value : 16 * 1024 * 1024);

            int bytes;
            while ((bytes = await stream.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytes);
                readBytes += bytes;
                if (totalBytes.HasValue)
                    progresser(readBytes, totalBytes.Value);
            }

            return output.ToArray();
        }

    }
}
