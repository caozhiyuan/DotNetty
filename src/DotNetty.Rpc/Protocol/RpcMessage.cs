namespace DotNetty.Rpc.Protocol
{
    using DotNetty.Rpc.Service;
    using Newtonsoft.Json;

    public class RpcMessage
    {
        public int RequestId { get; set; }

        public byte Type { get; set; }

        public string MessageId { get; set; }

        public short ErrorCode { get; set; }

        public string ErrorMsg { get; set; }

        public byte[] Message { get; set; }
    }

    public enum RpcMessageType : byte
    {
        Req = 1,
        Res = 2,
        PingReq = 3,
        PingRes = 4
    }
}
