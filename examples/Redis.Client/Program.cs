﻿using System;

namespace Redis.Client
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Codecs.Redis;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Examples.Common;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ExampleHelper.SetConsoleLogger();

     
                while (true)
                {
         

                    Task.WaitAll(RedisTest(),RedisTest(),RedisTest(),RedisTest());

                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        static MultithreadEventLoopGroup group = new MultithreadEventLoopGroup();

        static async Task RedisTest()
        {
            var b = new Bootstrap();
            b.Group(group)
                .Channel<TcpSocketChannel>()
                .Handler(
                    new ActionChannelInitializer<ISocketChannel>(
                        channel =>
                        {
                            IChannelPipeline p = channel.Pipeline;
                            p.AddLast(new RedisDecoder());
                            p.AddLast(new RedisBulkStringAggregator());
                            p.AddLast(new RedisArrayAggregator());
                            p.AddLast(new RedisEncoder());
                            p.AddLast(new RedisClientHandler());
                        }));

            IChannel ch = await b.ConnectAsync("10.1.62.66", 6379);
            var clientRpcHandler = ch.Pipeline.Get<RedisClientHandler>();

            await clientRpcHandler.SendRequest(new[] { "get", "Suiyi.ProductData.Models.Products.Product_257" });

            var sw = new Stopwatch();
            sw.Start();

            RunTest(ch);

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        static void RunTest(IChannel ch)
        {
            var clientRpcHandler = ch.Pipeline.Get<RedisClientHandler>();

            int count = 25000;
            var cde = new CountdownEvent(count);

            Parallel.For(0, count,
                (i) =>
                {
                    Task<object> task = clientRpcHandler.SendRequest(new[] { "get", "Suiyi.ProductData.Models.Products.Product_257" });
                    task.ContinueWith(
                        n =>
                        {
                            if (n.IsFaulted)
                            {
                                Console.WriteLine(n);
                            }
                            cde.Signal();
                        });
                });

            cde.Wait();
        }
    }

    public class RedisClientHandler : ChannelHandlerAdapter
    {
        private readonly ConcurrentQueue<TaskCompletionSource<object>> pendingRpc;
        private volatile IChannel channel;

        public RedisClientHandler()
        {
            this.pendingRpc = new ConcurrentQueue<TaskCompletionSource<object>>();
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
            this.channel = context.Channel;
        }

        public override void ChannelRead(IChannelHandlerContext context, object msg)
        {
            try
            {
                object res = this.ParseAggregatedRedisResponse(msg);
                if (this.pendingRpc.TryDequeue(out TaskCompletionSource<object> tsc))
                {
                    tsc.SetResult(res);
                }
            }
            finally
            {
                ReferenceCountUtil.Release(msg);
            }
        }

        private struct RedisResponse
        {
            public int RedisMessageType { get; set; }

            public object Content { get; set; }

            public long Integer { get; set; }
        }

        object ParseAggregatedRedisResponse(object msg)
        {
            switch (msg)
            {
                case SimpleStringRedisMessage message:
                    string content = message.Content;
                    return new RedisResponse
                    {
                        Content = content
                    };
                case ErrorRedisMessage _:
                    string errmsg = ((ErrorRedisMessage)msg).Content;
                    return new RedisResponse
                    {
                        Content = errmsg
                    };
                case IntegerRedisMessage _:
                    long i = ((IntegerRedisMessage)msg).Value;
                    return new RedisResponse
                    {
                        Integer = i
                    };
                case IFullBulkStringRedisMessage _:
                    string str = GetString((IFullBulkStringRedisMessage)msg);
                    return new RedisResponse
                    {
                        Content = str
                    };
                case ArrayRedisMessage _:
                    var redisresponses = new List<object>();
                    foreach (IRedisMessage child in ((ArrayRedisMessage)msg).Children)
                    {
                        redisresponses.Add(this.ParseAggregatedRedisResponse(child));
                    }
                    return new RedisResponse
                    {
                        Content = redisresponses
                    };
                default:
                    throw new CodecException("unknown message type: " + msg);
            }
        }

        private static string GetString(IFullBulkStringRedisMessage msg)
        {
            if (msg.IsNull)
            {
                return "(null)";
            }
            return msg.Content.ToString(Encoding.UTF8);
        }

        public Task<object> SendRequest(string[] commands)
        {
            var tcs = new TaskCompletionSource<object>();

            this.pendingRpc.Enqueue(tcs);
            
            var children = new List<IRedisMessage>(commands.Length);
            foreach (string cmdString in commands)
            {
                children.Add(new FullBulkStringRedisMessage(ByteBufferUtil.EncodeString(this.channel.Allocator, cmdString, Encoding.UTF8)));
            }
            IRedisMessage request = new ArrayRedisMessage(children);
            this.channel.WriteAndFlushAsync(request);

            return tcs.Task;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }
    }
}
