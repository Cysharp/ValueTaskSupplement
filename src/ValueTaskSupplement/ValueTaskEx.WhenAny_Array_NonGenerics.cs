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
        public static ValueTask<int> WhenAny(ValueTask left, Task right)
        {
            return new ValueTask<int>(new WhenAnyPromise2(left, new ValueTask(right)), 0);
        }

        public static ValueTask<int> WhenAny(IEnumerable<ValueTask> tasks)
        {
            return new ValueTask<int>(new WhenAnyPromiseAll(tasks), 0);
        }

        class WhenAnyPromiseAll : IValueTaskSource<int>
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

            int winArgumentIndex = -1;

            public WhenAnyPromiseAll(IEnumerable<ValueTask> tasks)
            {
                var i = 0;
                foreach (var task in tasks)
                {
                    var awaiter = task.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
                            TryInvokeContinuationWithIndex(i);
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

            void RegisterContinuation(ValueTaskAwaiter awaiter, int index)
            {
                awaiter.UnsafeOnCompleted(() =>
                {
                    try
                    {
                        awaiter.GetResult();
                        TryInvokeContinuationWithIndex(index);
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

            void TryInvokeContinuationWithIndex(int winIndex)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
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

            public int GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return winArgumentIndex;
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (Volatile.Read(ref winArgumentIndex) != -1) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAnyPromiseAll)state;
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
                var self = (WhenAnyPromiseAll)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }
    }
}
