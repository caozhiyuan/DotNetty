﻿namespace DotNetty.Rpc.Server
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
                    var rpcResponse = new RpcMessage
                    {
                        RequestId = state.Item2.RequestId
                    };

                    if (request.RequestId == "ping")
                    {
                        rpcResponse.Message = new Pong();
                    }
                    else
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
                    }

                    IChannelHandlerContext ctx = state.Item1;
                    WriteAndFlushAsync(ctx, rpcResponse);
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
                        Logger.Debug("ReaderIdle context.CloseAsync");
                    }
                    context.CloseAsync();
                }
                else if (e.State == IdleState.WriterIdle)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("WriterIdle context.CloseAsync");
                    }
                    context.CloseAsync();
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
