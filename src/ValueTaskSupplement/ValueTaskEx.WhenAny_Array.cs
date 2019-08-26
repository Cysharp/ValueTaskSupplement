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
        public static ValueTask<(bool hasResultLeft, T result)> WhenAny<T>(ValueTask<T> left, Task right)
        {
            return new ValueTask<(bool hasResultLeft, T result)>(new WhenAnyPromiseBinary<T>(left, new ValueTask(right)), 0);
        }

        public static ValueTask<(bool hasResultLeft, T result)> WhenAny<T>(ValueTask<T> left, ValueTask right)
        {
            return new ValueTask<(bool hasResultLeft, T result)>(new WhenAnyPromiseBinary<T>(left, right), 0);
        }

        public static ValueTask<(int winArgumentIndex, T result)> WhenAny<T>(IEnumerable<ValueTask<T>> tasks)
        {
            return new ValueTask<(int, T)>(new WhenAnyPromiseAll<T>(tasks), 0);
        }

        class WhenAnyPromiseBinary<T> : IValueTaskSource<(bool hasResultLeft, T result)>
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

            public WhenAnyPromiseBinary(ValueTask<T> left, ValueTask right)
            {
                {
                    var awaiter = left.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            var taskResult = awaiter.GetResult();
                            TryInvokeContinuationWithResult(taskResult, 0);
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
                        RegisterContinuation(awaiter, 0);
                    }
                }
                {
                    var awaiter = right.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
                            TryInvokeContinuationWithResult(default(T), 1);
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
                        RegisterContinuation(awaiter, 1);
                    }
                }
            }

            void RegisterContinuation(ValueTaskAwaiter awaiter, int index)
            {
                awaiter.UnsafeOnCompleted(() =>
                {
                    try
                    {
                        awaiter.GetResult();
                        TryInvokeContinuationWithResult(default(T), index);
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

            void RegisterContinuation(ValueTaskAwaiter<T> awaiter, int index)
            {
                awaiter.UnsafeOnCompleted(() =>
                {
                    try
                    {
                        var taskResult = awaiter.GetResult();
                        TryInvokeContinuationWithResult(taskResult, index);
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

            public (bool, T) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (winArgumentIndex == 0, result);
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
                var t = (Tuple<Action<object>, WhenAnyPromiseBinary<T>>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromiseBinary<T>>)state;
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
                            var taskResult = awaiter.GetResult();
                            TryInvokeContinuationWithResult(taskResult, i);
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
                        var taskResult = awaiter.GetResult();
                        TryInvokeContinuationWithResult(taskResult, index);
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
    }
}
