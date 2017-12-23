using System;
using System.Threading.Tasks;

namespace Rpc.Client
{
    using System.Diagnostics;
    using System.Threading;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Rpc.Client;
    using Microsoft.Extensions.Logging.Console;
    using Rpc.Models.Test;

    public class Program
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("Program");

        public static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("io.netty.leakDetection.level", "Disabled");

            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => true, false));

            try
            {
                while (true)
                {
                    int threadNum = 16;
                    int requestNum = 10000;
                    var sw = new Stopwatch();
                    sw.Start();

                    var threads = new Thread[threadNum];
                    for (int i = 0; i < threadNum; ++i)
                    {
                        threads[i] = new Thread(Test)
                        {
                            IsBackground = true
                        }; 
                        threads[i].Start(requestNum);
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.ReadKey();
        }

        static void Test(object obj)
        {
            int count = Convert.ToInt32(obj);
            string serverAddress = "10.1.4.204:9008";

            var cde = new CountdownEvent(count);
            for (int i = 0; i < count; i++)
            {
                NettyClient client = NettyClientFactory.Get(serverAddress);
                var query = new TestCityQuery
                {
                    Id = i
                };
                Task<CityInfo> task = client.SendRequest(query);
                task.ContinueWith(n =>
                {
                    if (n.IsFaulted)
                    {
                        Logger.Error(n.Exception);
                    }
                    cde.Signal();
                });
            }

            cde.Wait();
        }
    }
}
