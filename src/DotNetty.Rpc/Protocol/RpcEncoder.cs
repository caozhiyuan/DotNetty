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
            if (input.Message == null)
            {
                input.MessageId = string.Empty;
            }
            else
            {
                Type msgType = input.Message.GetType();
                input.MessageId = msgType.FullName;
            }

            byte[] midBytes = Encoding.UTF8.GetBytes(input.MessageId);
            int midLen = midBytes.Length;

            byte[] eMsgBytes = Encoding.UTF8.GetBytes(input.ErrorMsg ?? string.Empty);
            int eMsgLen = eMsgBytes.Length;

            int headLength = 4 + 1 + 2 + midLen + 2 + 2 + eMsgLen;
            if (headLength > short.MaxValue)
            {
                throw new Exception("headLength too long");
            }

            IByteBuffer headerBuffer = context.Allocator.Buffer(headLength);
            headerBuffer.WriteInt(input.RequestId);
            headerBuffer.WriteByte(input.Type);
            headerBuffer.WriteShort(midLen);
            headerBuffer.WriteBytes(midBytes);
            headerBuffer.WriteShort(input.ErrorCode);
            headerBuffer.WriteShort(eMsgLen);
            headerBuffer.WriteBytes(eMsgBytes);

            IByteBuffer message = ByteBufferUtil.EncodeString(
                context.Allocator,
                SerializationUtil.MessageSerialize(input.Message),
                Encoding.UTF8);

            int messageLength = message.ReadableBytes;

            IByteBuffer lenBuffer = context.Allocator.Buffer(6);
            lenBuffer.WriteInt(2 + headLength + messageLength);
            lenBuffer.WriteShort(headLength);

            output.Add(lenBuffer);

            output.Add(headerBuffer);

            output.Add(message);
        }
    }
}
