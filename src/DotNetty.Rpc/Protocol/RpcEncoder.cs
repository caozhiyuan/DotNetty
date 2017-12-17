namespace DotNetty.Rpc.Protocol
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class RpcEncoder<T> : MessageToMessageEncoder<T>
    {

        protected override void Encode(IChannelHandlerContext context, T input, List<object> output)
        {
            string messageStr = SerializationUtil.Serialize(input);
            IByteBuffer message = ByteBufferUtil.EncodeString(context.Allocator,
                messageStr,
                Encoding.UTF8);

            int length = message.ReadableBytes;
            output.Add(context.Allocator.Buffer(4).WriteInt(length));

            output.Add(message);
        }
    }
}
