using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ValueTaskSupplement
{
    public static partial class ValueTaskEx
    {
        public static ValueTask<T> FromResult<T>(T result)
        {
            return new ValueTask<T>(result);
        }

        public static ValueTask<T> Lazy<T>(Func<ValueTask<T>> factory)
        {
            return new ValueTask<T>(new AsyncLazySource<T>(factory), 0);
        }

        public static ValueTask<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> tasks)
        {
            return new ValueTask<T[]>(new WhenAllPromiseAll<T>(tasks), 0);
        }

        public static ValueTask<(int winArgumentIndex, T result)> WhenAny<T>(IEnumerable<ValueTask<T>> tasks)
        {
            return new ValueTask<(int, T)>(new WhenAnyPromiseAll<T>(tasks), 0);
        }

        class WhenAllPromiseAll<T> : IValueTaskSource<T[]>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            T[] result;

            public WhenAllPromiseAll(IEnumerable<ValueTask<T>> tasks)
            {
                if (tasks is ValueTask<T>[] array)
                {
                    Run(array);
                    return;
                }
                if (tasks is IReadOnlyCollection<ValueTask<T>> c)
                {
                    Run(c, c.Count);
                    return;
                }
                if (tasks is ICollection<ValueTask<T>> c2)
                {
                    Run(c2, c2.Count);
                    return;
                }

                var list = new TempList<ValueTask<T>>(99);
                try
                {
                    foreach (var item in tasks)
                    {
                        list.Add(item);
                    }

                    Run(list.AsSpan());
                }
                finally
                {
                    list.Dispose();
                }
            }

            void Run(ReadOnlySpan<ValueTask<T>> tasks)
            {
                result = new T[tasks.Length];

                var i = 0;
                foreach (var task in tasks)
                {
                    var awaiter = task.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            result[i] = awaiter.GetResult();
                        }
                        catch (Exception ex)
                        {
                            exception = ExceptionDispatchInfo.Capture(ex);
                            return;
                        }
                        TryInvokeContinuationWithIncrement();
                    }
                    else
                    {
                        RegisterContinuation(awaiter, i);
                    }

                    i++;
                }
            }

            void Run(IEnumerable<ValueTask<T>> tasks, int length)
            {
                result = new T[length];

                var i = 0;
                foreach (var task in tasks)
                {
                    var awaiter = task.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            result[i] = awaiter.GetResult();
                        }
                        catch (Exception ex)
                        {
                            exception = ExceptionDispatchInfo.Capture(ex);
                            return;
                        }
                        TryInvokeContinuationWithIncrement();
                    }
                    else
                    {
                        RegisterContinuation(awaiter, i);
                    }

                    i++;
                }
            }

            void RegisterContinuation(ValueTaskAwaiter<T> awaiter, int index)
            {
                awaiter.UnsafeOnCompleted(() =>
                {
                    try
                    {
                        result[index] = awaiter.GetResult();
                    }
                    catch (Exception ex)
                    {
                        exception = ExceptionDispatchInfo.Capture(ex);
                        TryInvokeContinuation();
                        return;
                    }
                    TryInvokeContinuationWithIncrement();
                });
            }

            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == result.Length)
                {
                    TryInvokeContinuation();
                }
            }

            void TryInvokeContinuation()
            {
                var c = Interlocked.Exchange(ref continuation, ContinuationSentinel.CompletedContinuation);
                if (c != ContinuationSentinel.AvailableContinuation && c != ContinuationSentinel.CompletedContinuation)
                {
                    var spinWait = new SpinWait();
                    while (state == null) // worst case, state is not set yet so wait.
                    {
                        spinWait.SpinOnce();
                    }

                    if (execContext != null)
                    {
                        ExecutionContext.Run(execContext, execContextCallback, Tuple.Create(c, this));
                    }
                    else if (syncContext != null)
                    {
                        syncContext.Post(syncContextCallback, Tuple.Create(c, this));
                    }
                    else
                    {
                        c(state);
                    }
                }
            }

            public T[] GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return result;
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == result.Length) ? ValueTaskSourceStatus.Succeeded
                    : (exception != null) ? ((exception.SourceException is OperationCanceledException) ? ValueTaskSourceStatus.Canceled : ValueTaskSourceStatus.Faulted)
                    : ValueTaskSourceStatus.Pending;
            }

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                if (Interlocked.CompareExchange(ref this.continuation, continuation, ContinuationSentinel.AvailableContinuation) != ContinuationSentinel.AvailableContinuation)
                {
                    throw new InvalidOperationException("does not allow multiple await.");
                }

                this.state = state;
                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
                {
                    execContext = ExecutionContext.Capture();
                }
                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
                {
                    syncContext = SynchronizationContext.Current;
                }

                if (GetStatus(token) != ValueTaskSourceStatus.Pending)
                {
                    TryInvokeContinuation();
                }
            }

            static void ExecutionContextCallback(object state)
            {
                var t = (Tuple<Action<object>, WhenAllPromiseAll<T>>)state;
                var self = t.Item2;
                if (self.syncContext != null)
                {
                    SynchronizationContextCallback(state);
                }
                else
                {
                    var invokeState = self.state;
                    self.state = null;
                    t.Item1.Invoke(invokeState);
                }
            }

            static void SynchronizationContextCallback(object state)
            {
                var t = (Tuple<Action<object>, WhenAllPromiseAll<T>>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        class WhenAnyPromiseAll<T> : IValueTaskSource<(int winArgumentIndex, T result)>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            T result;
            int winArgumentIndex = -1;

            public WhenAnyPromiseAll(IEnumerable<ValueTask<T>> tasks)
            {
                var i = 0;
                foreach (var task in tasks)
                {
                    var awaiter = task.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            result = awaiter.GetResult();
                            TryInvokeContinuationWithResult(result, i);
                            return;
                        }
                        catch (Exception ex)
                        {
                            exception = ExceptionDispatchInfo.Capture(ex);
                            return;
                        }
                    }
                    else
                    {
                        RegisterContinuation(awaiter, i);
                    }

                    i++;
                }
            }

            void RegisterContinuation(ValueTaskAwaiter<T> awaiter, int index)
            {
                awaiter.UnsafeOnCompleted(() =>
                {
                    try
                    {
                        result = awaiter.GetResult();
                        TryInvokeContinuationWithResult(result, index);
                        return;
                    }
                    catch (Exception ex)
                    {
                        exception = ExceptionDispatchInfo.Capture(ex);
                        TryInvokeContinuation();
                        return;
                    }
                });
            }

            void TryInvokeContinuationWithResult(T result, int winIndex)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    this.result = result;
                    Volatile.Write(ref winArgumentIndex, winIndex);
                    TryInvokeContinuation();
                }
            }

            void TryInvokeContinuation()
            {
                var c = Interlocked.Exchange(ref continuation, ContinuationSentinel.CompletedContinuation);
                if (c != ContinuationSentinel.AvailableContinuation && c != ContinuationSentinel.CompletedContinuation)
                {
                    var spinWait = new SpinWait();
                    while (state == null) // worst case, state is not set yet so wait.
                    {
                        spinWait.SpinOnce();
                    }

                    if (execContext != null)
                    {
                        ExecutionContext.Run(execContext, execContextCallback, Tuple.Create(c, this));
                    }
                    else if (syncContext != null)
                    {
                        syncContext.Post(syncContextCallback, Tuple.Create(c, this));
                    }
                    else
                    {
                        c(state);
                    }
                }
            }

            public (int, T) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (winArgumentIndex, result);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (Volatile.Read(ref winArgumentIndex) != -1) ? ValueTaskSourceStatus.Succeeded
                    : (exception != null) ? ((exception.SourceException is OperationCanceledException) ? ValueTaskSourceStatus.Canceled : ValueTaskSourceStatus.Faulted)
                    : ValueTaskSourceStatus.Pending;
            }

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                if (Interlocked.CompareExchange(ref this.continuation, continuation, ContinuationSentinel.AvailableContinuation) != ContinuationSentinel.AvailableContinuation)
                {
                    throw new InvalidOperationException("does not allow multiple await.");
                }

                this.state = state;
                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
                {
                    execContext = ExecutionContext.Capture();
                }
                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
                {
                    syncContext = SynchronizationContext.Current;
                }

                if (GetStatus(token) != ValueTaskSourceStatus.Pending)
                {
                    TryInvokeContinuation();
                }
            }

            static void ExecutionContextCallback(object state)
            {
                var t = (Tuple<Action<object>, WhenAnyPromiseAll<T>>)state;
                var self = t.Item2;
                if (self.syncContext != null)
                {
                    SynchronizationContextCallback(state);
                }
                else
                {
                    var invokeState = self.state;
                    self.state = null;
                    t.Item1.Invoke(invokeState);
                }
            }

            static void SynchronizationContextCallback(object state)
            {
                var t = (Tuple<Action<object>, WhenAnyPromiseAll<T>>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        class AsyncLazySource<T> : IValueTaskSource<T>
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

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                var task = GetSource();
                if (task.IsCompleted)
                {
                    continuation(state);
                }
                OnCompletedSlow(task, continuation, state, flags);
            }

            static async void OnCompletedSlow(ValueTask<T> source, Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags)
            {
                ExecutionContext execContext = null;
                SynchronizationContext syncContext = null;
                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
                {
                    execContext = ExecutionContext.Capture();
                }
                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
                {
                    syncContext = SynchronizationContext.Current;
                }

                await source.ConfigureAwait(false);

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
