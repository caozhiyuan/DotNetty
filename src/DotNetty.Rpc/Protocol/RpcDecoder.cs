namespace DotNetty.Rpc.Protocol
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Channels;

    public class RpcDecoder : ByteToMessageDecoder
    {
        readonly ConcurrentDictionary<string, Type> messageTypes;

        public RpcDecoder(ConcurrentDictionary<string, Type> messageTypes)
        {
            this.messageTypes = messageTypes;
        }

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

            var rpcMessage = new RpcMessage
            {
                RequestId = requestId,
                Type = rpcType,
                MessageId = messageId,
                ErrorCode = errorCode,
                ErrorMsg = errorMsg
            };
            
            rpcMessage.MessageId = messageId;
            if (this.messageTypes.TryGetValue(messageId, out Type type))
            {
                int msgLen = dataLength - headerLen - 2;
                string str = input.ToString(input.ReaderIndex, msgLen, Encoding.UTF8);
                input.SetReaderIndex(input.ReaderIndex + msgLen);

                rpcMessage.Message = SerializationUtil.MessageDeserialize(str, type);
            }
            else
            {
                rpcMessage.Message = default(IMessage);
            }

            output.Add(rpcMessage);
        }
    }
}
