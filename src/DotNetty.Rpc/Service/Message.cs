namespace DotNetty.Rpc.Service
{

    public abstract class AbsMessage<T> : IMessage<T>
        where T : IMessage
    {
        public T ReturnValue { get; set; }
    }

    public interface IMessage<T>: IMessage
        where T : IMessage
    {
        T ReturnValue { get; set; }
    }

    public interface IMessage
    {
    }

    public class Ping : AbsMessage<Pong>
    {
    }

    public class Pong : IMessage
    {

    }
}
