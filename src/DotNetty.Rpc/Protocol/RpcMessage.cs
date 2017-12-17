namespace DotNetty.Rpc.Protocol
{
    using DotNetty.Rpc.Service;

    public class RpcMessage
    {
        public string RequestId { get; set; }

        public byte MessageType { get; set; }

        public string MessageId { get; set; }

        public IMessage Message { get; set; }
    }

    public enum MessageType : byte
    {
        Request = 1,
        Response = 2
    }
}
