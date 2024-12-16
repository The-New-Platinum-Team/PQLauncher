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
        protected Uri m_address;
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

    }
}
