using System;
using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Text;
    using System.Threading;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class NettyClient
    {
        const int ConnectTimeout = 10000;
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("NettyClient");
        static readonly IEventLoopGroup WorkerGroup = new MultithreadEventLoopGroup();

        private readonly ManualResetEventSlim emptyEvent = new ManualResetEventSlim(false, 1);
        private Bootstrap bootstrap;
        private RpcClientHandler clientRpcHandler;

        internal void Connect(EndPoint socketAddress)
        {
            ConcurrentDictionary<string, Type> messageTypes = Registrations.MessageTypes;

            this.bootstrap = new Bootstrap();
            this.bootstrap.Group(WorkerGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Option(ChannelOption.SoKeepalive, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(c =>
                {
                    IChannelPipeline pipeline = c.Pipeline;

                    pipeline.AddLast(new IdleStateHandler(60, 30, 0));
                    pipeline.AddLast(new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 0));
                    pipeline.AddLast(new RpcDecoder());
                    pipeline.AddLast(new RpcEncoder());

                    pipeline.AddLast(new ReconnectHandler(this.DoConnect));

                    pipeline.AddLast(new RpcClientHandler());
                }));

            this.DoConnect(socketAddress);
        }

        public async Task<T> SendRequest<T>(AbsMessage<T> request, int timeout = 10000)
        {
            this.WaitConnect();

            var rpcRequest = new RpcMessage
            {
                Message = Encoding.UTF8.GetBytes(SerializationUtil.Serialize(request)),
                Type = (byte)RpcMessageType.Req,
                MessageId = request.GetType().FullName
            };
            RpcMessage rpcReponse = await this.clientRpcHandler.SendRequest(rpcRequest, timeout);
            if (rpcReponse.ErrorCode > 0)
            {
                throw new Exception(rpcReponse.ErrorMsg);
            }
            if (rpcReponse.Message == null)
            {
                return default(T);
            }
            return (T)SerializationUtil.Deserialize(Encoding.UTF8.GetString(rpcReponse.Message), typeof(T));
        }

        void WaitConnect()
        {
            if (this.clientRpcHandler == null || !this.clientRpcHandler.GetChannel().Active)
            {
                if (!this.emptyEvent.Wait(ConnectTimeout))
                {
                    throw new Handlers.TimeoutException("Channel Connect TimeOut");
                }
            }
            if (this.clientRpcHandler == null)
            {
                throw new Exception("ClientRpcHandler Null");
            }
        }

        private Task DoConnect(EndPoint socketAddress)
        {
            this.emptyEvent.Reset();
;
            Task<IChannel> task = this.bootstrap.ConnectAsync(socketAddress);
            return task.ContinueWith(n =>
            {
                if (n.IsFaulted || n.IsCanceled)
                {
                    Logger.Info("NettyClient connected to {} failed", socketAddress);
                    if (this.clientRpcHandler != null)
                    {
                        IChannel channel0 = this.clientRpcHandler.GetChannel();
                        channel0.EventLoop.Schedule(_ => this.DoConnect((EndPoint)_), socketAddress, TimeSpan.FromMilliseconds(1000));
                    }
                    else
                    {
                        WorkerGroup.GetNext().Schedule(_ => this.DoConnect((EndPoint)_), socketAddress, TimeSpan.FromMilliseconds(1000));
                    }
                }
                else
                {
                    Logger.Info("NettyClient connected to {}", socketAddress);
                    this.clientRpcHandler = n.Result.Pipeline.Get<RpcClientHandler>();
					this.emptyEvent.Set();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
