namespace DotNetty.Rpc.Protocol
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using DotNetty.Rpc.Service;
    using Newtonsoft.Json;

    public class SerializationUtil
    {
        static readonly JsonSerializerSettings DefaultJsonSerializerSetting = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public static string Serialize(IMessage obj)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(DefaultJsonSerializerSetting);
            var stringWriter = new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);
            using (var jsonTextWriter = new JsonTextWriter(stringWriter))
            {
                jsonTextWriter.Formatting = jsonSerializer.Formatting;
                jsonSerializer.Serialize(jsonTextWriter, obj, obj.GetType());
            }
            return stringWriter.ToString();
        }

        public static T Deserialize<T>(byte[] data, Type type)
        {
            try
            {
                string s = Encoding.UTF8.GetString(data);
                return (T)JsonConvert.DeserializeObject(s, type, DefaultJsonSerializerSetting);
            }
            catch
            {
                return default(T);
            }
        }
    }
}
