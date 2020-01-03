using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ValueTaskSupplement
{
    public static partial class ValueTaskEx
    {
        public static AsyncLazy<T> Lazy<T>(Func<ValueTask<T>> factory)
        {
            return new AsyncLazy<T>(factory);
        }
    }

    public readonly struct AsyncLazy<T>
    {
        readonly ValueTask<T> innerTask;

        public AsyncLazy(Func<ValueTask<T>> factory)
        {
            innerTask = new ValueTask<T>(new AsyncLazySource(factory), 0);
        }

        public ValueTask<T> AsValueTask() => innerTask;

        public ValueTaskAwaiter<T> GetAwaiter() => innerTask.GetAwaiter();

        public static implicit operator ValueTask<T>(AsyncLazy<T> source)
        {
            return source.AsValueTask();
        }

        class AsyncLazySource : IValueTaskSource<T>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            Func<ValueTask<T>> factory;
            object syncLock;
            ValueTask<T> source;
            bool initialized;

            public AsyncLazySource(Func<ValueTask<T>> factory)
            {
                this.factory = factory;
                this.syncLock = new object();
            }

            ValueTask<T> GetSource()
            {
                return LazyInitializer.EnsureInitialized(ref source, ref initialized, ref syncLock, factory);
            }

            public T GetResult(short token)
            {
                return GetSource().Result;
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                var task = GetSource();
                return task.IsCompletedSuccessfully ? ValueTaskSourceStatus.Succeeded
                    : task.IsCanceled ? ValueTaskSourceStatus.Canceled
                    : task.IsFaulted ? ValueTaskSourceStatus.Faulted
                    : ValueTaskSourceStatus.Pending;
            }

            public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                var task = GetSource();
                if (task.IsCompleted)
                {
                    continuation(state);
                }
                else
                {
                    OnCompletedSlow(task, continuation, state, flags);
                }
            }

            static async void OnCompletedSlow(ValueTask<T> source, Action<object?> continuation, object? state, ValueTaskSourceOnCompletedFlags flags)
            {
                ExecutionContext? execContext = null;
                SynchronizationContext? syncContext = null;
                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
                {
                    execContext = ExecutionContext.Capture();
                }
                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
                {
                    syncContext = SynchronizationContext.Current;
                }

                try
                {
                    await source.ConfigureAwait(false);
                }
                catch { }

                if (execContext != null)
                {
                    ExecutionContext.Run(execContext, execContextCallback, Tuple.Create(continuation, state, syncContext));
                }
                else if (syncContext != null)
                {
                    syncContext.Post(syncContextCallback, Tuple.Create(continuation, state, syncContext));
                }
                else
                {
                    continuation(state);
                }
            }

            static void ExecutionContextCallback(object state)
            {
                var t = (Tuple<Action<object>, object, SynchronizationContext>)state;
                if (t.Item3 != null)
                {
                    SynchronizationContextCallback(state);
                }
                else
                {
                    t.Item1.Invoke(t.Item2);
                }
            }

            static void SynchronizationContextCallback(object state)
            {
                var t = (Tuple<Action<object>, object, SynchronizationContext>)state;
                t.Item1.Invoke(t.Item2);
            }
        }
    }
}