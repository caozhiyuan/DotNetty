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
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("NettyClient");
        static readonly IEventLoopGroup WorkerGroup = new MultithreadEventLoopGroup();
        
        private readonly Bootstrap bootstrap;
        private RpcClientHandler clientRpcHandler;
        private readonly EndPoint endPoint;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public NettyClient(EndPoint endPoint)
        {
            this.endPoint = endPoint;
            this.bootstrap = new Bootstrap();
            this.bootstrap.Group(WorkerGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Option(ChannelOption.SoKeepalive, true)
                .Option(ChannelOption.SoReuseaddr, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(c =>
                {
                    IChannelPipeline pipeline = c.Pipeline;

                    pipeline.AddLast(new IdleStateHandler(60, 30, 0));
                    pipeline.AddLast(new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 0));
                    pipeline.AddLast(new RpcDecoder());
                    pipeline.AddLast(new RpcEncoder());

                    pipeline.AddLast(new ReconnectHandler(this.DoConnectIfNeed));

                    pipeline.AddLast(new RpcClientHandler());
                }));

        }

        private bool IsChannelInactive
        {
            get
            {
                IChannel channel = this.clientRpcHandler?.GetChannel();
                if (channel == null)
                {
                    return true;
                }
                return !channel.Active;
            }
        }

        private async Task DoConnectIfNeed(EndPoint socketAddress)
        {
            if (this.IsChannelInactive)
            {
                await this.semaphoreSlim.WaitAsync();
                try
                {
                    if (this.IsChannelInactive)
                    {
                        IChannel channel = await this.bootstrap.ConnectAsync(socketAddress);
                        this.clientRpcHandler = channel.Pipeline.Get<RpcClientHandler>();
                    }
                }
                finally
                {
                    this.semaphoreSlim.Release();
                }
            }
        }

        public async Task<T> SendRequest<T>(AbsMessage<T> request, int timeout = 10000)
        {
            await this.DoConnectIfNeed(this.endPoint);

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
    }
}
