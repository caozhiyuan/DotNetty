namespace DotNetty.Rpc.Service
{

    public abstract class AbsMessage<T> : IMessage<T>
    {
        public T ReturnValue { get; set; }
    }

    public interface IMessage<T>: IMessage
    {
        T ReturnValue { get; set; }
    }

    public interface IMessage
    {
    }
}
