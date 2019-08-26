using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ValueTaskSupplement
{
    internal static class ContinuationSentinel
    {
        public static readonly Action<object> AvailableContinuation = _ => { };
        public static readonly Action<object> CompletedContinuation = _ => { };
    }

    //public static class ValueTaskEx
    //{
    //    public static ValueTask<(T0, T1, T2)> WhenAll<T0, T1, T2>(ValueTask<T0> t0, ValueTask<T1> t1, ValueTask<T2> t2)
    //    {
    //        if (t0.IsCompletedSuccessfully && t1.IsCompletedSuccessfully && t2.IsCompletedSuccessfully)
    //        {
    //            return new ValueTask<(T0, T1, T2)>((t0.Result, t1.Result, t2.Result));
    //        }

    //        return new ValueTask<(T0, T1, T2)>(new WhenAllPromise<T0, T1, T2>(t0, t1, t2), 0);
    //    }

    //    class WhenAllPromise<T0, T1, T2> : IValueTaskSource<(T0, T1, T2)>
    //    {
    //        const int ResultCount = 3;
    //        static readonly ContextCallback execContextCallback = ExecutionContextCallback;
    //        static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

    //        T0 t0 = default(T0);
    //        T1 t1 = default(T1);
    //        T2 t2 = default(T2);
    //        int completedCount = 0;
    //        ExceptionDispatchInfo exception;
    //        ValueTaskAwaiter<T0> awaiter0;
    //        ValueTaskAwaiter<T1> awaiter1;
    //        ValueTaskAwaiter<T2> awaiter2;

    //        Action<object> continuation = ContinuationSentinel.AvailableContinuation;
    //        object state;
    //        SynchronizationContext syncContext;
    //        ExecutionContext execContext;

    //        public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2)
    //        {
    //            {
    //                var awaiter = task0.GetAwaiter();
    //                if (awaiter.IsCompleted)
    //                {
    //                    try
    //                    {
    //                        t0 = awaiter.GetResult();
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        exception = ExceptionDispatchInfo.Capture(ex);
    //                        return;
    //                    }
    //                    TryInvokeContinuationWithIncrement();
    //                }
    //                else
    //                {
    //                    awaiter0 = awaiter;
    //                    awaiter.UnsafeOnCompleted(ContinuationT0);
    //                }
    //            }
    //            {
    //                var awaiter = task1.GetAwaiter();
    //                if (awaiter.IsCompleted)
    //                {
    //                    try
    //                    {
    //                        t1 = awaiter.GetResult();
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        exception = ExceptionDispatchInfo.Capture(ex);
    //                        return;
    //                    }
    //                    TryInvokeContinuationWithIncrement();
    //                }
    //                else
    //                {
    //                    awaiter1 = awaiter;
    //                    awaiter.UnsafeOnCompleted(ContinuationT1);
    //                }
    //            }
    //            {
    //                var awaiter = task2.GetAwaiter();
    //                if (awaiter.IsCompleted)
    //                {
    //                    try
    //                    {
    //                        t2 = awaiter.GetResult();
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        exception = ExceptionDispatchInfo.Capture(ex);
    //                        return;
    //                    }
    //                    TryInvokeContinuationWithIncrement();
    //                }
    //                else
    //                {
    //                    awaiter2 = awaiter;
    //                    awaiter.UnsafeOnCompleted(ContinuationT2);
    //                }
    //            }
    //        }

    //        void ContinuationT0()
    //        {
    //            try
    //            {
    //                t0 = awaiter0.GetResult();
    //            }
    //            catch (Exception ex)
    //            {
    //                exception = ExceptionDispatchInfo.Capture(ex);
    //                TryInvokeContinuation();
    //                return;
    //            }
    //            TryInvokeContinuationWithIncrement();
    //        }

    //        void ContinuationT1()
    //        {
    //            try
    //            {
    //                t1 = awaiter1.GetResult();
    //            }
    //            catch (Exception ex)
    //            {
    //                exception = ExceptionDispatchInfo.Capture(ex);
    //                TryInvokeContinuation();
    //                return;
    //            }
    //            TryInvokeContinuationWithIncrement();
    //        }

    //        void ContinuationT2()
    //        {
    //            try
    //            {
    //                t2 = awaiter2.GetResult();
    //            }
    //            catch (Exception ex)
    //            {
    //                exception = ExceptionDispatchInfo.Capture(ex);
    //                TryInvokeContinuation();
    //                return;
    //            }
    //            TryInvokeContinuationWithIncrement();
    //        }

    //        void TryInvokeContinuationWithIncrement()
    //        {
    //            if (Interlocked.Increment(ref completedCount) == ResultCount)
    //            {
    //                TryInvokeContinuation();
    //            }
    //        }

    //        void TryInvokeContinuation()
    //        {
    //            var c = Interlocked.Exchange(ref continuation, ContinuationSentinel.CompletedContinuation);
    //            if (c != ContinuationSentinel.AvailableContinuation && c != ContinuationSentinel.CompletedContinuation)
    //            {
    //                var spinWait = new SpinWait();
    //                while (state == null) // worst case, state is not set yet so wait.
    //                {
    //                    spinWait.SpinOnce();
    //                }

    //                if (execContext != null)
    //                {
    //                    ExecutionContext.Run(execContext, execContextCallback, Tuple.Create(c, this));
    //                }
    //                else if (syncContext != null)
    //                {
    //                    syncContext.Post(syncContextCallback, Tuple.Create(c, this));
    //                }
    //                else
    //                {
    //                    c(state);
    //                }
    //            }
    //        }

    //        public (T0, T1, T2) GetResult(short token)
    //        {
    //            if (exception != null)
    //            {
    //                exception.Throw();
    //            }
    //            return (t0, t1, t2);
    //        }

    //        public ValueTaskSourceStatus GetStatus(short token)
    //        {
    //            return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
    //                : (exception != null) ? ((exception.SourceException is OperationCanceledException) ? ValueTaskSourceStatus.Canceled : ValueTaskSourceStatus.Faulted)
    //                : ValueTaskSourceStatus.Pending;
    //        }

    //        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
    //        {
    //            if (Interlocked.CompareExchange(ref this.continuation, continuation, ContinuationSentinel.AvailableContinuation) != ContinuationSentinel.AvailableContinuation)
    //            {
    //                throw new InvalidOperationException("does not allow multiple await.");
    //            }

    //            this.state = state;
    //            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
    //            {
    //                execContext = ExecutionContext.Capture();
    //            }
    //            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
    //            {
    //                syncContext = SynchronizationContext.Current;
    //            }

    //            if (GetStatus(token) != ValueTaskSourceStatus.Pending)
    //            {
    //                TryInvokeContinuation();
    //            }
    //        }

    //        static void ExecutionContextCallback(object state)
    //        {
    //            var t = (Tuple<Action<object>, WhenAllPromise<T0, T1, T2>>)state;
    //            var self = t.Item2;
    //            if (self.syncContext != null)
    //            {
    //                SynchronizationContextCallback(state);
    //            }
    //            else
    //            {
    //                var invokeState = self.state;
    //                self.state = null;
    //                t.Item1.Invoke(invokeState);
    //            }
    //        }

    //        static void SynchronizationContextCallback(object state)
    //        {
    //            var t = (Tuple<Action<object>, WhenAllPromise<T0, T1, T2>>)state;
    //            var self = t.Item2;
    //            var invokeState = self.state;
    //            self.state = null;
    //            t.Item1.Invoke(invokeState);
    //        }
    //    }

    //}
}
