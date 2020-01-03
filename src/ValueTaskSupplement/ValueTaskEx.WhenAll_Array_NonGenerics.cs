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
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

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
                this.state = state;

                if (GetStatus(token) != ValueTaskSourceStatus.Pending)
                {
                    TryInvokeContinuation();
                }
            }

            static void ExecutionContextCallback(object state)
            {
                var self = (WhenAllPromiseAll)state;
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
                var self = (WhenAllPromiseAll)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

    }
}