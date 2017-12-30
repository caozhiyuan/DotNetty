// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Client
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Examples.Common;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    class Program
    {

        static void Main() => new NettyClient().RunClientAsync().Wait();
    }

    public class NettyClient
    {
        public async Task RunClientAsync()
        {
            ExampleHelper.SetConsoleLogger();

            var group = new MultithreadEventLoopGroup();

            try
            {
                var bootstrap = new Bootstrap();
                bootstrap
                    .Group(group)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.TcpNodelay, true)
                    .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;

                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(4));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4));

                        pipeline.AddLast(new StringEncoder());
                        pipeline.AddLast(new StringDecoder());

                        pipeline.AddLast("echo", new EchoClientHandler());
                    }));

                IChannel clientChannel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));
                var clientRpcHandler = clientChannel.Pipeline.Get<EchoClientHandler>();

                this.Test(Tuple.Create(clientRpcHandler, 10));

                while (true)
                {
                    int threadNum = 16;
                    int requestNum = 10000;
                    var sw = new Stopwatch();
                    sw.Start();

                    var threads = new Thread[threadNum];
                    for (int i = 0; i < threadNum; ++i)
                    {
                        threads[i] = new Thread(this.Test)
                        {
                            IsBackground = true
                        };
                        threads[i].Start(Tuple.Create(clientRpcHandler, requestNum));
                    }

                    foreach (Thread t in threads)
                    {
                        t.Join();
                    }

                    sw.Stop();
                    long timeCost = sw.ElapsedMilliseconds;
                    string msg = string.Format("Async call total-time-cost:{0}ms, req/s={1}", timeCost, ((double)(requestNum * threadNum)) / timeCost * 1000);
                    Console.WriteLine(msg);
                }
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }

        void Test(object obj)
        {
            var tuple = (Tuple<EchoClientHandler, int>)obj;
            int count = Convert.ToInt32(tuple.Item2);
            var cde = new CountdownEvent(count);
            for (int i = 0; i < count; i++)
            {
                var rpcRequest = new RpcMessage
                {
                    RequestId = this.RequestId.ToString(),
                    Message = "[{\"OldPictureId\":57457,\"PictureId\":57456,\"LinkType\":1,\"LinkContent\":\"https://wechat.34580.com/zhuanti/2017.12/chufang/index.html\",\"BeginTime\":\"2017-12-14T00:00:01\",\"EndTime\":\"2017-12-31T23:59:59\"}]"
                };
                Task<RpcMessage> task = tuple.Item1.SendRequest(rpcRequest);
                task.ContinueWith(n =>
                {
                    if (n.IsFaulted)
                    {
                        Console.WriteLine(n);
                    }
                    cde.Signal();
                });
            }

            cde.Wait();
        }

        int requestId = 1;

        int RequestId
        {
            get { return Interlocked.Increment(ref this.requestId); }
        }
    }
}