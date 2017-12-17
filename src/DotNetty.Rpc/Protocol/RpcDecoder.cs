namespace DotNetty.Rpc.Protocol
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
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

            int headerLen = input.ReadInt();

            byte[] header;
            using (var ms = new MemoryStream())
            {
                input.ReadBytes(ms, headerLen);
                header = ms.ToArray();
            }
            string headerStr = Encoding.UTF8.GetString(header);

            string[] headers = headerStr.Split('$');
            var rpcMessage = new RpcMessage
            {
                RequestId = headers[0],
                MessageType = Convert.ToByte(headers[1])
            };

            string messageId = headers[2];
            if (this.messageTypes.TryGetValue(messageId, out Type type))
            {
                byte[] message;
                using (var ms = new MemoryStream())
                {
                    input.ReadBytes(ms, dataLength - headerLen - 4);
                    message = ms.ToArray();
                }
                rpcMessage.Message = SerializationUtil.MessageDeserialize(message, type);
            }
            else
            {
                rpcMessage.Message = default(IMessage);
            }

            output.Add(rpcMessage);
        }
    }
}
