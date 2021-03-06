﻿namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class RpcServer
    {
        readonly string ipAndPort;

        public RpcServer(string ipAndPort)
        {
            this.ipAndPort = ipAndPort;
        }

        public async Task StartAsync()
        {
            IModule[] modules = Registrations.FindModules();
            foreach (IModule module in modules)
            {
                module.Initialize();
            }

            var bossGroup = new MultithreadEventLoopGroup(1);
            var workerGroup = new MultithreadEventLoopGroup();

            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 1024)
                    .Option(ChannelOption.SoReuseaddr, true)
                    .ChildOption(ChannelOption.SoKeepalive, true)
                    .ChildOption(ChannelOption.SoReuseaddr, true)
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast(new IdleStateHandler(180, 120, 0));
                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 0));
                        pipeline.AddLast(new RpcDecoder());
                        pipeline.AddLast(new RpcEncoder());
                        pipeline.AddLast(new RpcHandler());
                    }));

                string[] parts = this.ipAndPort.Split(':');
                int port = int.Parse(parts[1]);

                IChannel bootstrapChannel = await bootstrap.BindAsync(port);

                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                Task.WaitAll(bossGroup.ShutdownGracefullyAsync(), workerGroup.ShutdownGracefullyAsync());
            }
        }
    }
}
