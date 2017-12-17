namespace DotNetty.Rpc.Protocol
{
    using DotNetty.Rpc.Service;

    public class RpcMessage
    {
        public string RequestId { get; set; }

        public string MessageId { get; set; }

        public IMessage Message { get; set; }
    }
}
