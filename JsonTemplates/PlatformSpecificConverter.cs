using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PQLauncher.JsonTemplates
{
    class PlatformSpecificConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                IDictionary<String, T> dict = serializer.Deserialize<IDictionary<String, T>>(reader);
                var ourPlatform = Platform.PlatformToString(Platform.OSPlatform);
                return dict[ourPlatform];
            }
            catch (Exception e)
            {
                return serializer.Deserialize<T>(reader);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
