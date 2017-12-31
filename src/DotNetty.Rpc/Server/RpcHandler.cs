namespace DotNetty.Rpc.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Channels;

    public class RpcHandler: ChannelHandlerAdapter
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("RpcHandler");

        readonly ConcurrentDictionary<string, Type> messageTypes;

        public RpcHandler()
        {
            this.messageTypes =  Registrations.MessageTypes;
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var request = (RpcMessage) message;
            Task.Factory.StartNew(
                async o =>
                {
                    var state = (Tuple<IChannelHandlerContext, RpcMessage>) o;
                    IChannelHandlerContext ctx = state.Item1;
                    RpcMessage req = state.Item2;
                    if (req.Type == (byte)RpcMessageType.Req)
                    {
                        var rpcResponse = new RpcMessage
                        {
                            RequestId = req.RequestId,
                            Type = (byte)RpcMessageType.Res
                        };
                        try
                        {
                            string messageId = req.MessageId;
                            if (this.messageTypes.TryGetValue(messageId, out Type type))
                            {
                                object rpcRequest = SerializationUtil.Deserialize(Encoding.UTF8.GetString(req.Message), type);
                                object msg = await ServiceBus.Instance.Publish(rpcRequest);
                                string str = SerializationUtil.Serialize(msg);
                                rpcResponse.Message = Encoding.UTF8.GetBytes(str);
                                rpcResponse.MessageId = msg.GetType().FullName;
                            }
                            else
                            {
                                rpcResponse.ErrorCode = 404;
                                rpcResponse.ErrorMsg = "Not Found";
                            }
                        }
                        catch (Exception ex)
                        {
                            rpcResponse.ErrorCode = 500;
                            rpcResponse.ErrorMsg = ex.Message;
                        }

                        WriteAndFlushAsync(ctx, rpcResponse);
                    }
                    else if (req.Type == (byte)RpcMessageType.PingReq)
                    {
                        var rpcResponse = new RpcMessage
                        {
                            Type = (byte)RpcMessageType.PingRes
                        };

                        WriteAndFlushAsync(ctx, rpcResponse);
                    }
                    else if (req.Type == (byte)RpcMessageType.PingRes)
                    {
                        if (Logger.DebugEnabled)
                        {
                            Logger.Debug("get client ping response");
                        }
                    }
                },
                Tuple.Create(context, request),
                default(CancellationToken),
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        /// <summary>
        /// WriteAndFlushAsync
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="rpcResponse"></param>
        /// <returns></returns>
        static void WriteAndFlushAsync(IChannelHandlerContext ctx, RpcMessage rpcResponse)
        {
            ctx.WriteAndFlushAsync(rpcResponse);
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is IdleStateEvent)
            {
                var e = (IdleStateEvent)evt;
                if (e.State == IdleState.ReaderIdle)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("WriterIdle context.CloseAsync");
                    }
                    context.CloseAsync();
                }
                else if (e.State == IdleState.WriterIdle)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("WriterIdle send ping request ");
                    }

                    context.WriteAndFlushAsync(
                        new RpcMessage
                        {
                            Type = (byte)RpcMessageType.PingReq
                        });
                }
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Logger.Error(exception);

            context.CloseAsync();
        }
    }
}
