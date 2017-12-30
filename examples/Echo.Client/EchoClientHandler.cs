// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Newtonsoft.Json;

    public class RpcMessage
    {
        public string RequestId { get; set; }

        public string Message { get; set; }
    }

    public class EchoClientHandler : ChannelHandlerAdapter
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("RpcClientHandler");
        private readonly ConcurrentDictionary<string, TaskCompletionSource<RpcMessage>> pendingRpc;
        private volatile IChannel channel;
        private EndPoint remotePeer;

        public EchoClientHandler()
        {
            this.pendingRpc = new ConcurrentDictionary<string, TaskCompletionSource<RpcMessage>>();
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

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            string str = (string)message;
            var rpcMessage = JsonConvert.DeserializeObject<RpcMessage>(str);
            string requestId = rpcMessage.RequestId;
            this.pendingRpc.TryGetValue(requestId, out TaskCompletionSource<RpcMessage> tsc);
            if (tsc != null)
            {
                this.pendingRpc.TryRemove(requestId, out tsc);
                tsc.SetResult(rpcMessage);
            }
        }

        public Task<RpcMessage> SendRequest(RpcMessage request)
        {
            var tcs = new TaskCompletionSource<RpcMessage>();

            this.pendingRpc.TryAdd(request.RequestId, tcs);

            this.channel.WriteAndFlushAsync(JsonConvert.SerializeObject(request));

            return tcs.Task;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }
    }
}