using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ValueTaskSupplement
{
    public static partial class ValueTaskEx
    {
        public static AsyncLazy Lazy(Func<ValueTask> factory)
        {
            return new AsyncLazy(factory);
        }
    }

    public readonly struct AsyncLazy
    {
        readonly ValueTask innerTask;

        public AsyncLazy(Func<ValueTask> factory)
        {
            innerTask = new ValueTask(new AsyncLazySource(factory), 0);
        }

        public ValueTask AsValueTask() => innerTask;

        public ValueTaskAwaiter GetAwaiter() => innerTask.GetAwaiter();

        public static implicit operator ValueTask(AsyncLazy source)
        {
            return source.AsValueTask();
        }

        class AsyncLazySource : IValueTaskSource
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            Func<ValueTask> factory;
            object syncLock;
            ValueTask source;
            bool initialized;

            public AsyncLazySource(Func<ValueTask> factory)
            {
                this.factory = factory;
                this.syncLock = new object();
            }

            ValueTask GetSource()
            {
                return LazyInitializer.EnsureInitialized(ref source, ref initialized, ref syncLock, factory);
            }

            public void GetResult(short token)
            {
                GetSource().GetAwaiter().GetResult();
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

            static async void OnCompletedSlow(ValueTask source, Action<object?> continuation, object? state, ValueTaskSourceOnCompletedFlags flags)
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