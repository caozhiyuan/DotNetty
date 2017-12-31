namespace DotNetty.Rpc.Server
{
    using System;
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
                            IMessage rpcRequest = state.Item2.Message;
                            if (rpcRequest == null)
                            {
                                rpcResponse.ErrorCode = 404;
                                rpcResponse.ErrorMsg = "Not Found";
                            }
                            else
                            {
                                rpcResponse.Message = (IMessage)await ServiceBus.Instance.Publish(rpcRequest);
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
                            Type = (byte)RpcMessageType.PingRes,
                            Message = new Pong()
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
                            Type = (byte)RpcMessageType.PingReq,
                            Message = new Ping()
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
