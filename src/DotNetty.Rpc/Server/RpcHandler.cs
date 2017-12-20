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
                    if (!string.IsNullOrEmpty(req.RequestId))
                    {
                        var rpcResponse = new RpcMessage
                        {
                            RequestId = req.RequestId
                        };
                        if (!req.RequestId.StartsWith("#"))
                        {
                            var res = new Result();
                            try
                            {
                                IMessage rpcRequest = state.Item2.Message;
                                if (rpcRequest == null)
                                {
                                    res.Error = "404";
                                }
                                else
                                {
                                    res.Data = await ServiceBus.Instance.Publish(rpcRequest);
                                }
                            }
                            catch (Exception ex)
                            {
                                res.Error = ex.Message;
                            }
                            rpcResponse.Message = res;
                            WriteAndFlushAsync(ctx, rpcResponse);
                        }
                        else
                        {
                            if (req.RequestId == "#ping")
                            {
                                rpcResponse.Message = new Pong();
                                WriteAndFlushAsync(ctx, rpcResponse);
                            }
                            else if (req.RequestId == "#sping")
                            {
                                if (Logger.DebugEnabled)
                                {
                                    Logger.Debug("get client sping response");
                                }
                            }
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
                        Logger.Debug("WriterIdle send sping request ");
                    }

                    context.WriteAndFlushAsync(new RpcMessage
                    {
                        RequestId = "#sping",
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
