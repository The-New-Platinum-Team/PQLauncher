using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PQLauncher.JsonTemplates
{
    class DownloadedPlatformSpecificConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                IDictionary<String, Uri> dict = serializer.Deserialize<IDictionary<String, Uri>>(reader);

                var ourPlatform = Platform.PlatformToString(Platform.OSPlatform);
                Uri address = dict[ourPlatform];
                return new DownloadedField<T>(address);
            }
            catch (Exception e)
            {
                Uri address = serializer.Deserialize<Uri>(reader);
                return new DownloadedField<T>(address);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
