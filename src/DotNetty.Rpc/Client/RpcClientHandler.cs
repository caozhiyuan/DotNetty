namespace DotNetty.Rpc.Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Channels;

    public class RpcClientHandler : ChannelHandlerAdapter
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("RpcClientHandler");
        private readonly ConcurrentDictionary<int, RequestContext> pendingRpc;
        private volatile IChannel channel;
        private EndPoint remotePeer;

        public RpcClientHandler()
        {
            this.pendingRpc = new ConcurrentDictionary<int, RequestContext>();
        }

        public IChannel GetChannel() => this.channel;

        public EndPoint GetRemotePeer() => this.remotePeer;

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            this.remotePeer = this.channel.RemoteAddress;
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
            this.channel = context.Channel;
        }


        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            var rpcMessage = (RpcMessage)message;
            if (rpcMessage.Type == (byte)RpcMessageType.Res)
            {
                int requestId = rpcMessage.RequestId;
                RequestContext requestContext;
                this.pendingRpc.TryGetValue(requestId, out requestContext);
                if (requestContext != null)
                {
                    this.pendingRpc.TryRemove(requestId, out requestContext);
                    requestContext.TaskCompletionSource.SetResult(rpcMessage);
                    requestContext.TimeOutTimer.Cancel();
                }
            }
            else if (rpcMessage.Type == (byte)RpcMessageType.PingReq)
            {
                if (Logger.DebugEnabled)
                {
                    Logger.Debug("get server ping request ");
                }

                ctx.WriteAndFlushAsync(
                    new RpcMessage
                    {
                        Type = (byte)RpcMessageType.PingRes
                    });
            }
        }

        int requestId0;

        int RequestId
        {
            get { return Interlocked.Increment(ref this.requestId0); }
        }

        public Task<RpcMessage> SendRequest(RpcMessage request, int timeout = 10000)
        {
            request.RequestId = this.RequestId;

            var tcs = new TaskCompletionSource<RpcMessage>();

            IScheduledTask timeOutTimer = this.channel.EventLoop.Schedule(n => this.GetRpcResponseTimeOut(n), request, TimeSpan.FromMilliseconds(timeout));

            var context = new RequestContext(tcs, timeOutTimer);

            this.pendingRpc.TryAdd(request.RequestId, context);

            this.channel.WriteAndFlushAsync(request);

            return tcs.Task;
        }

        void GetRpcResponseTimeOut(object n)
        {
            int requestId = ((RpcMessage)n).RequestId;
            RequestContext requestContext;
            this.pendingRpc.TryGetValue(requestId, out requestContext);
            if (requestContext != null)
            {
                this.pendingRpc.TryRemove(requestId, out requestContext);
                requestContext.TaskCompletionSource.SetException(new Handlers.TimeoutException("Get RpcResponse TimeOut"));
            }
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
