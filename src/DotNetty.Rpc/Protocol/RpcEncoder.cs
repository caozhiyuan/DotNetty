namespace DotNetty.Rpc.Protocol
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class RpcEncoder : MessageToMessageEncoder<RpcMessage>
    {
        protected override void Encode(IChannelHandlerContext context, RpcMessage input, List<object> output)
        {
            Type msgType = input.Message.GetType();

            input.MessageId = msgType.FullName;
            string headerStr = $"{input.RequestId}${input.MessageId}";
            IByteBuffer header = ByteBufferUtil.EncodeString(context.Allocator,
                headerStr,
                Encoding.UTF8);

            int headLength = header.ReadableBytes;

            IByteBuffer message = ByteBufferUtil.EncodeString(
                context.Allocator,
                SerializationUtil.MessageSerialize(input.Message, msgType),
                Encoding.UTF8);

            int messageLength = message.ReadableBytes;

            output.Add(context.Allocator.Buffer(4).WriteInt(4 + headLength + messageLength));

            output.Add(context.Allocator.Buffer(4).WriteInt(headLength));

            output.Add(header);

            output.Add(message);
        }
    }
}
