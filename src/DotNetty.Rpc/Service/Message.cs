namespace DotNetty.Rpc.Service
{

    public abstract class AbsMessage<T> : IMessage<T>
        where T : new()
    {
        public T ReturnValue { get; set; }
    }

    public interface IMessage<T>: IMessage
        where T : new()
    {
        T ReturnValue { get; set; }
    }

    public interface IMessage
    {
      
    }
}
