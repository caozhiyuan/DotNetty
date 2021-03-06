﻿
namespace DotNetty.Rpc.Service
{
    using System.Collections.Concurrent;
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    internal class ServiceBus
    {
        static readonly ServiceBus Instance0 = new ServiceBus();

        internal static ServiceBus Instance
        {
            get { return Instance0; }
        }

        internal ConcurrentDictionary<Type, Func<object,Task>> DynamicMethods { get; private set; } = new ConcurrentDictionary<Type, Func<object, Task>>();

        internal void Subscribe<T>(Listener<T> subscriber) where T : IMessage
        {
            Func<object, Task> func;
            Type type = typeof(T);
            if (this.DynamicMethods.TryGetValue(type, out func))
            {
                throw new Exception("Mutil Listener For this IMessage");
            }

            ParameterExpression r = Expression.Parameter(typeof(object));
            Expression c = Expression.Call(Expression.Constant(subscriber.Target), subscriber.Method, Expression.Convert(r, type));

            IEnumerable<ParameterExpression> parameters = new[] { r };
            func = Expression.Lambda<Func<object, Task>>(c, parameters).Compile();
            this.DynamicMethods.TryAdd(type, func);
        }

        internal async Task<object> Publish(object eventArgs)
        {
            Func<object, Task> handler;
            Type type = eventArgs.GetType();
            if (this.DynamicMethods.TryGetValue(type, out handler))
            {          
                await handler(eventArgs);
                return this.ReturnValue(eventArgs, type);
            }
            throw new NotImplementedException(string.Format("NotImplementedException:{0}", type.Name));
        }


        private ConcurrentDictionary<Type, Func<object, object>> DynamicReturnValueMethods { get; set; } = new ConcurrentDictionary<Type, Func<object, object>>();

        private object ReturnValue(object eventArgs, Type eventType)
        {
            Func<object, object> @delegate = this.DynamicReturnValueMethods.GetOrAdd(eventType, (t) =>
            {
                ParameterExpression r = Expression.Parameter(typeof(object));
                Expression p = Expression.Convert(r, t);
                MemberExpression cm = Expression.Property(p, "ReturnValue");
                return Expression.Lambda<Func<object, object>>(cm, r).Compile();
            });
            return @delegate(eventArgs);
        }
    }
}
