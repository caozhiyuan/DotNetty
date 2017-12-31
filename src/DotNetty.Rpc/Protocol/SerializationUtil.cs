namespace DotNetty.Rpc.Protocol
{
    using System;
    using DotNetty.Rpc.Service;
    using Newtonsoft.Json;

    public class SerializationUtil
    {
        static readonly JsonSerializerSettings DefaultJsonSerializerSetting = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, DefaultJsonSerializerSetting);
        }

        public static object Deserialize(string str, Type type)
        {
            return JsonConvert.DeserializeObject(str, type, DefaultJsonSerializerSetting);
        }
    }
}
