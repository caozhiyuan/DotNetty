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

        public static string MessageSerialize(IMessage obj)
        {
            return JsonConvert.SerializeObject(obj, DefaultJsonSerializerSetting);
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
