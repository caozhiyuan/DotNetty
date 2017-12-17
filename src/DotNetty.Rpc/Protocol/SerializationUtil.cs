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

        public static string MessageSerialize(IMessage obj, Type type)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(DefaultJsonSerializerSetting);
            var stringWriter = new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);
            using (var jsonTextWriter = new JsonTextWriter(stringWriter))
            {
                jsonTextWriter.Formatting = jsonSerializer.Formatting;
                jsonSerializer.Serialize(jsonTextWriter, obj, type);
            }
            return stringWriter.ToString();
        }

        public static IMessage MessageDeserialize(string str, Type type)
        {
            try
            {
                return (IMessage)JsonConvert.DeserializeObject(str, type, DefaultJsonSerializerSetting);
            }
            catch
            {
                return default(IMessage);
            }
        }
    }
}
