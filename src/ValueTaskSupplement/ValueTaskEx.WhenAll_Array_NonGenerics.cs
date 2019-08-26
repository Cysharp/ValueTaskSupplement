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
        public static ValueTask WhenAll(IEnumerable<ValueTask> tasks)
        {
            return new ValueTask(new WhenAllPromiseAll(tasks), 0);
        }

        class WhenAllPromiseAll : IValueTaskSource
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;
            
            int taskCount = 0;
            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromiseAll(IEnumerable<ValueTask> tasks)
            {
                if (tasks is ValueTask[] array)
                {
                    Run(array);
                    return;
                }
                if (tasks is IReadOnlyCollection<ValueTask> c)
                {
                    Run(c, c.Count);
                    return;
                }
                if (tasks is ICollection<ValueTask> c2)
                {
                    Run(c2, c2.Count);
                    return;
                }

                var list = new TempList<ValueTask>(99);
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

            void Run(ReadOnlySpan<ValueTask> tasks)
            {
                taskCount = tasks.Length;

                var i = 0;
                foreach (var task in tasks)
                {
                    var awaiter = task.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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

            void Run(IEnumerable<ValueTask> tasks, int length)
            {
                taskCount = length;

                var i = 0;
                foreach (var task in tasks)
                {
                    var awaiter = task.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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

            void RegisterContinuation(ValueTaskAwaiter awaiter, int index)
            {
                awaiter.UnsafeOnCompleted(() =>
                {
                    try
                    {
                        awaiter.GetResult();
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
                if (Interlocked.Increment(ref completedCount) == taskCount)
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

            public void GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == taskCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromiseAll>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromiseAll>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

    }
}