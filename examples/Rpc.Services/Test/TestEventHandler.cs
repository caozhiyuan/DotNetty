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
                Name = "{\"Id\":1,\"CityId\":1,\"CityFlag\":\"sz\",\"WebApiUrl\":\"https://api1.34580.com/\",\"ImageSiteUrl\":\"http://picpro-sz.34580.com/\",\"CityName\":\"苏州市\"}Hello world"
            };
            return Task.FromResult(eventData);
        }
    }
}
