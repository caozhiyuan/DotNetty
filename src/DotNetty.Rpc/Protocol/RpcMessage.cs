namespace DotNetty.Rpc.Protocol
{
    using DotNetty.Rpc.Service;

    public class RpcMessage
    {
        public string RequestId { get; set; }

        public string MessageId { get; set; }

        public byte Type { get; set; }

        public IMessage Message { get; set; }
    }

    public enum RpcMessageType:byte
    {
        Req = 0,
        Res = 1
    }
}
