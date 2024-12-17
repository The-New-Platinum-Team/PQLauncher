using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PQLauncher.JsonTemplates
{
    public class DownloadedField<T>
    {
        public Uri m_address;
        public bool ready;
        public T value;

        public DownloadedField(Uri address)
        {
            m_address = address;
            ready = false;
        }

        /// <summary>
        /// Download the file this field is assigned to
        /// </summary>
        /// <returns>If the download was successful</returns>
        async public Task<bool> Download()
        {
            var client = new HttpClient();
           
            try
            {
                var json = await client.GetStringAsync(m_address);
                if (typeof(T) == typeof(string))
                {
                    value = (T)(object)json;
                    ready = true;
                    return true;
                }
                value = JsonConvert.DeserializeObject<T>(json);

                ready = true;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Download the file this field is assigned to
        /// </summary>
        /// <returns>If the download was successful</returns>
        async public Task<bool> DownloadWithProgress(Action<long, long> progresser)
        {
            var client = new HttpClient();

            try
            {
                // Must use ResponseHeadersRead to avoid buffering of the content
                using (var response = await client.GetAsync(m_address, HttpCompletionOption.ResponseHeadersRead))
                {
                    // You must use as stream to have control over buffering and number of bytes read/received
                    var chunks = new List<byte[]>();
                    long readBytes = 0;
                    long? totalBytes = response.Content.Headers.ContentLength;
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        // Read/process bytes from stream as appropriate

                        // long bytesRecieved = //...
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
                        return false;
                    }
                    var output = new byte[chunks.Sum(arr => arr.Length)];
                    int writeIdx = 0;
                    foreach (var byteArr in chunks)
                    {
                        byteArr.CopyTo(output, writeIdx);
                        writeIdx += byteArr.Length;
                    }
                    var jstring = Encoding.UTF8.GetString(output);
                    if (typeof(T) == typeof(string))
                    {
                        value = (T)(object)jstring;
                        ready = true;
                        return true;
                    }
                    value = JsonConvert.DeserializeObject<T>(jstring);
                    ready = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

    }
}
