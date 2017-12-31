namespace Rpc.Services.Test
{
    using System;
    using DotNetty.Rpc.Service;
    using Rpc.Models;
    using System.Threading.Tasks;
    using Rpc.Models.Test;

    public class TestEventHandler: EventHandlerImpl
    {
        protected override void InitializeComponents()
        {
            this.AddEventListener<TestCityQuery>(this.Handler);
            this.AddEventListener<TestAddressQuery>(this.Handler);
        }

        private Task<TestAddressQuery> Handler(TestAddressQuery eventData)
        {
            eventData.ReturnValue = new TestAddressQuery()
            {
                Id = eventData.Id
            };
            return Task.FromResult(eventData);
        }

        private Task<TestCityQuery> Handler(TestCityQuery eventData)
        {
            eventData.ReturnValue = new CityInfo()
            {
                Id = eventData.Id,
                Name = @"Hello world"
            };
            return Task.FromResult(eventData);
        }
    }
}
