namespace DotNetty.Rpc.Protocol
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class RpcEncoder : MessageToMessageEncoder<RpcMessage>
    {
        protected override void Encode(IChannelHandlerContext context, RpcMessage input, List<object> output)
        {
            string headerStr = $"{input.RequestId}${input.MessageType}${input.MessageId}";
            IByteBuffer header = ByteBufferUtil.EncodeString(context.Allocator,
                headerStr,
                Encoding.UTF8);

            int headLength = header.ReadableBytes;

            IByteBuffer message = ByteBufferUtil.EncodeString(context.Allocator,
                SerializationUtil.Serialize(input.Message),
                Encoding.UTF8);

            int messageLength = message.ReadableBytes;

            output.Add(context.Allocator.Buffer(4).WriteInt(4 + headLength + messageLength));

            output.Add(context.Allocator.Buffer(4).WriteInt(headLength));

            output.Add(header);

            output.Add(message);
        }
    }
}
