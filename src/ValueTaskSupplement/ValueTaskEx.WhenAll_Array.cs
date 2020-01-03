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
        public static ValueTask<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> tasks)
        {
            return new ValueTask<T[]>(new WhenAllPromiseAll<T>(tasks), 0);
        }

        class WhenAllPromiseAll<T> : IValueTaskSource<T[]>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            T[] result;

            public WhenAllPromiseAll(IEnumerable<ValueTask<T>> tasks)
            {
                if (tasks is ValueTask<T>[] array)
                {
                    result = CreateArray(array.Length);
                    Run(array);
                    return;
                }
                if (tasks is IReadOnlyCollection<ValueTask<T>> c)
                {
                    result = CreateArray(c.Count);
                    Run(c, c.Count);
                    return;
                }
                if (tasks is ICollection<ValueTask<T>> c2)
                {
                    result = CreateArray(c2.Count);
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

                    var span = list.AsSpan();
                    result = CreateArray(span.Length);
                    Run(span);
                }
                finally
                {
                    list.Dispose();
                }
            }

            T[] CreateArray(int length)
            {
                if (length == 0) return Array.Empty<T>();
                return new T[length];
            }

            void Run(ReadOnlySpan<ValueTask<T>> tasks)
            {
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

            void Run(IEnumerable<ValueTask<T>> tasks, int _)
            {
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
                // allow capture lambda
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
                        invokeContinuation = c;
                        ExecutionContext.Run(execContext, execContextCallback, this);
                    }
                    else if (syncContext != null)
                    {
                        invokeContinuation = c;
                        syncContext.Post(syncContextCallback, this);
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

            public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                var c = Interlocked.CompareExchange(ref this.continuation, continuation, ContinuationSentinel.AvailableContinuation);
                if (c == ContinuationSentinel.CompletedContinuation)
                {
                    continuation(state);
                    return;
                }

                if (c != ContinuationSentinel.AvailableContinuation)
                {
                    throw new InvalidOperationException("does not allow multiple await.");
                }

                if (state == null)
                {
                    throw new InvalidOperationException("invalid state.");
                }

                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
                {
                    execContext = ExecutionContext.Capture();
                }
                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
                {
                    syncContext = SynchronizationContext.Current;
                }
                this.state = state; // go signal

                if (GetStatus(token) != ValueTaskSourceStatus.Pending)
                {
                    TryInvokeContinuation();
                }
            }

            static void ExecutionContextCallback(object state)
            {
                var self = (WhenAllPromiseAll<T>)state;
                if (self.syncContext != null)
                {
                    self.syncContext.Post(syncContextCallback, self);
                }
                else
                {
                    var invokeContinuation = self.invokeContinuation!;
                    var invokeState = self.state;
                    self.invokeContinuation = null;
                    self.state = null;
                    invokeContinuation(invokeState);
                }
            }

            static void SynchronizationContextCallback(object state)
            {
                var self = (WhenAllPromiseAll<T>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }
    }
}