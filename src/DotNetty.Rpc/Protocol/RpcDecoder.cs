namespace DotNetty.Rpc.Protocol
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class RpcDecoder : ByteToMessageDecoder
    {
        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (input.ReadableBytes < 4)
            {
                return;
            }
            input.MarkReaderIndex();

            int dataLength = input.ReadInt();

            if (input.ReadableBytes < dataLength)
            {
                input.ResetReaderIndex();
                return;
            }

            short headerLen = input.ReadShort();   
            int requestId = input.ReadInt();
            byte rpcType = input.ReadByte();


            int mIdLen = input.ReadShort();
            string messageId = input.ToString(input.ReaderIndex, mIdLen, Encoding.UTF8);
            input.SetReaderIndex(input.ReaderIndex + mIdLen);

            short errorCode = input.ReadShort();
            short errorMsgLen = input.ReadShort();
            string errorMsg = input.ToString(input.ReaderIndex, errorMsgLen, Encoding.UTF8);
            input.SetReaderIndex(input.ReaderIndex + errorMsgLen);

            int msgLen = dataLength - headerLen - 2;
            var message = new byte[msgLen];
            input.ReadBytes(message);

            var rpcMessage = new RpcMessage
            {
                RequestId = requestId,
                Type = rpcType,
                MessageId = messageId,
                ErrorCode = errorCode,
                ErrorMsg = errorMsg,
                Message = message
            };

            output.Add(rpcMessage);
        }
    }
}
