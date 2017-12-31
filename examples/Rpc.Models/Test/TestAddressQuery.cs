namespace Rpc.Models.Test
{
    using DotNetty.Rpc.Service;

    public class TestAddressQuery : AbsMessage<TestAddressQuery>
    {
        public int Id { get; set; }
    }
}
