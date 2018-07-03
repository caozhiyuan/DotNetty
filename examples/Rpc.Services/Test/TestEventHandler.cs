namespace Rpc.Services.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using DotNetty.Rpc.Service;
    using System.Threading.Tasks;
    using Rpc.Models.Test;
    using StackExchange.Redis;

    public class TestEventHandler: EventHandlerImpl
    {
        protected override void InitializeComponents()
        {
            this.AddEventListener<TestCityQuery>(this.Handler);
            this.AddEventListener<TestAddressQuery>(this.Handler);
        }

        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { "192.168.1.104:6379" }
        });

        private Task<TestAddressQuery> Handler(TestAddressQuery eventData)
        {
            eventData.ReturnValue = new TestAddressQuery()
            {
                Id = eventData.Id
            };
            return Task.FromResult(eventData);
        }

        static readonly ConcurrentBag<string> Temps = new ConcurrentBag<string>();
        static System.Threading.Timer timer;

        static TestEventHandler()
        {
            ThreadPool.SetMaxThreads(1024, 1024);
            timer = new Timer(Callback, null, 10000, 10000);
        }

        static void Callback(object state)
        {
            Console.WriteLine("Timer Callback");
            File.WriteAllLines("test.txt", Temps);
        }

        private async Task<TestCityQuery> Handler(TestCityQuery eventData)
        {
            var sw = new Stopwatch();
            sw.Start();
            IDatabase db = this.redis.GetDatabase();
            RedisValue str = await db.StringGetAsync("test");
            eventData.ReturnValue = new CityInfo()
            {
                Id = eventData.Id,
                Name = str
            };
            sw.Stop();
            Temps.Add(sw.ElapsedMilliseconds.ToString());
            return eventData;
        }


    }
}
