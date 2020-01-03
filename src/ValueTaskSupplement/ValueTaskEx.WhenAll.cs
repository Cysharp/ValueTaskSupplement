using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ValueTaskSupplement
{
    public static partial class ValueTaskEx
    {
        public static ValueTask<(T0, T1)> WhenAll<T0, T1>(ValueTask<T0> task0, ValueTask<T1> task1)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1)>((task0.Result, task1.Result));
            }

            return new ValueTask<(T0, T1)>(new WhenAllPromise<T0, T1>(task0, task1), 0);
        }

        class WhenAllPromise<T0, T1> : IValueTaskSource<(T0, T1)>
        {
            const int ResultCount = 2;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1>)state;
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
                var self = (WhenAllPromise<T0, T1>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2)> WhenAll<T0, T1, T2>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2)>((task0.Result, task1.Result, task2.Result));
            }

            return new ValueTask<(T0, T1, T2)>(new WhenAllPromise<T0, T1, T2>(task0, task1, task2), 0);
        }

        class WhenAllPromise<T0, T1, T2> : IValueTaskSource<(T0, T1, T2)>
        {
            const int ResultCount = 3;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2>)state;
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
                var self = (WhenAllPromise<T0, T1, T2>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3)> WhenAll<T0, T1, T2, T3>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3)>((task0.Result, task1.Result, task2.Result, task3.Result));
            }

            return new ValueTask<(T0, T1, T2, T3)>(new WhenAllPromise<T0, T1, T2, T3>(task0, task1, task2, task3), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3> : IValueTaskSource<(T0, T1, T2, T3)>
        {
            const int ResultCount = 4;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4)> WhenAll<T0, T1, T2, T3, T4>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4)>(new WhenAllPromise<T0, T1, T2, T3, T4>(task0, task1, task2, task3, task4), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4> : IValueTaskSource<(T0, T1, T2, T3, T4)>
        {
            const int ResultCount = 5;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5)> WhenAll<T0, T1, T2, T3, T4, T5>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5>(task0, task1, task2, task3, task4, task5), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5> : IValueTaskSource<(T0, T1, T2, T3, T4, T5)>
        {
            const int ResultCount = 6;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6)> WhenAll<T0, T1, T2, T3, T4, T5, T6>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6>(task0, task1, task2, task3, task4, task5, task6), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6)>
        {
            const int ResultCount = 7;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7>(task0, task1, task2, task3, task4, task5, task6, task7), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7)>
        {
            const int ResultCount = 8;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8>(task0, task1, task2, task3, task4, task5, task6, task7, task8), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8)>
        {
            const int ResultCount = 9;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)>
        {
            const int ResultCount = 10;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>
        {
            const int ResultCount = 11;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            T10 t10 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;
            ValueTaskAwaiter<T10> awaiter10;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t10 = awaiter.GetResult();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT10);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT10()
            {
                try
                {
                    t10 = awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result, task11.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>
        {
            const int ResultCount = 12;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            T10 t10 = default!;
            T11 t11 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;
            ValueTaskAwaiter<T10> awaiter10;
            ValueTaskAwaiter<T11> awaiter11;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t10 = awaiter.GetResult();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t11 = awaiter.GetResult();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT11);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT10()
            {
                try
                {
                    t10 = awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT11()
            {
                try
                {
                    t11 = awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result, task11.Result, task12.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>
        {
            const int ResultCount = 13;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            T10 t10 = default!;
            T11 t11 = default!;
            T12 t12 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;
            ValueTaskAwaiter<T10> awaiter10;
            ValueTaskAwaiter<T11> awaiter11;
            ValueTaskAwaiter<T12> awaiter12;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t10 = awaiter.GetResult();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t11 = awaiter.GetResult();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t12 = awaiter.GetResult();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT12);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT10()
            {
                try
                {
                    t10 = awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT11()
            {
                try
                {
                    t11 = awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT12()
            {
                try
                {
                    t12 = awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully && task13.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result, task11.Result, task12.Result, task13.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)>
        {
            const int ResultCount = 14;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            T10 t10 = default!;
            T11 t11 = default!;
            T12 t12 = default!;
            T13 t13 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;
            ValueTaskAwaiter<T10> awaiter10;
            ValueTaskAwaiter<T11> awaiter11;
            ValueTaskAwaiter<T12> awaiter12;
            ValueTaskAwaiter<T13> awaiter13;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t10 = awaiter.GetResult();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t11 = awaiter.GetResult();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t12 = awaiter.GetResult();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT12);
                    }
                }
                {
                    var awaiter = task13.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t13 = awaiter.GetResult();
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
                        awaiter13 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT13);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT10()
            {
                try
                {
                    t10 = awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT11()
            {
                try
                {
                    t11 = awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT12()
            {
                try
                {
                    t12 = awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT13()
            {
                try
                {
                    t13 = awaiter13.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully && task13.IsCompletedSuccessfully && task14.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result, task11.Result, task12.Result, task13.Result, task14.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)>
        {
            const int ResultCount = 15;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            T10 t10 = default!;
            T11 t11 = default!;
            T12 t12 = default!;
            T13 t13 = default!;
            T14 t14 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;
            ValueTaskAwaiter<T10> awaiter10;
            ValueTaskAwaiter<T11> awaiter11;
            ValueTaskAwaiter<T12> awaiter12;
            ValueTaskAwaiter<T13> awaiter13;
            ValueTaskAwaiter<T14> awaiter14;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t10 = awaiter.GetResult();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t11 = awaiter.GetResult();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t12 = awaiter.GetResult();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT12);
                    }
                }
                {
                    var awaiter = task13.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t13 = awaiter.GetResult();
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
                        awaiter13 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT13);
                    }
                }
                {
                    var awaiter = task14.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t14 = awaiter.GetResult();
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
                        awaiter14 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT14);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT10()
            {
                try
                {
                    t10 = awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT11()
            {
                try
                {
                    t11 = awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT12()
            {
                try
                {
                    t12 = awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT13()
            {
                try
                {
                    t13 = awaiter13.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT14()
            {
                try
                {
                    t14 = awaiter14.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully && task13.IsCompletedSuccessfully && task14.IsCompletedSuccessfully && task15.IsCompletedSuccessfully)
            {
                return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)>((task0.Result, task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result, task11.Result, task12.Result, task13.Result, task14.Result, task15.Result));
            }

            return new ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)>(new WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14, task15), 0);
        }

        class WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> : IValueTaskSource<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)>
        {
            const int ResultCount = 16;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            T3 t3 = default!;
            T4 t4 = default!;
            T5 t5 = default!;
            T6 t6 = default!;
            T7 t7 = default!;
            T8 t8 = default!;
            T9 t9 = default!;
            T10 t10 = default!;
            T11 t11 = default!;
            T12 t12 = default!;
            T13 t13 = default!;
            T14 t14 = default!;
            T15 t15 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;
            ValueTaskAwaiter<T3> awaiter3;
            ValueTaskAwaiter<T4> awaiter4;
            ValueTaskAwaiter<T5> awaiter5;
            ValueTaskAwaiter<T6> awaiter6;
            ValueTaskAwaiter<T7> awaiter7;
            ValueTaskAwaiter<T8> awaiter8;
            ValueTaskAwaiter<T9> awaiter9;
            ValueTaskAwaiter<T10> awaiter10;
            ValueTaskAwaiter<T11> awaiter11;
            ValueTaskAwaiter<T12> awaiter12;
            ValueTaskAwaiter<T13> awaiter13;
            ValueTaskAwaiter<T14> awaiter14;
            ValueTaskAwaiter<T15> awaiter15;

            int completedCount = 0;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAllPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t1 = awaiter.GetResult();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t2 = awaiter.GetResult();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t3 = awaiter.GetResult();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t4 = awaiter.GetResult();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t5 = awaiter.GetResult();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t6 = awaiter.GetResult();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t7 = awaiter.GetResult();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t8 = awaiter.GetResult();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t9 = awaiter.GetResult();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t10 = awaiter.GetResult();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t11 = awaiter.GetResult();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t12 = awaiter.GetResult();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT12);
                    }
                }
                {
                    var awaiter = task13.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t13 = awaiter.GetResult();
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
                        awaiter13 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT13);
                    }
                }
                {
                    var awaiter = task14.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t14 = awaiter.GetResult();
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
                        awaiter14 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT14);
                    }
                }
                {
                    var awaiter = task15.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t15 = awaiter.GetResult();
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
                        awaiter15 = awaiter;
                        awaiter.UnsafeOnCompleted(ContinuationT15);
                    }
                }
            }

            void ContinuationT0()
            {
                try
                {
                    t0 = awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT1()
            {
                try
                {
                    t1 = awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT2()
            {
                try
                {
                    t2 = awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT3()
            {
                try
                {
                    t3 = awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT4()
            {
                try
                {
                    t4 = awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT5()
            {
                try
                {
                    t5 = awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT6()
            {
                try
                {
                    t6 = awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT7()
            {
                try
                {
                    t7 = awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT8()
            {
                try
                {
                    t8 = awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT9()
            {
                try
                {
                    t9 = awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT10()
            {
                try
                {
                    t10 = awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT11()
            {
                try
                {
                    t11 = awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT12()
            {
                try
                {
                    t12 = awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT13()
            {
                try
                {
                    t13 = awaiter13.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT14()
            {
                try
                {
                    t14 = awaiter14.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void ContinuationT15()
            {
                try
                {
                    t15 = awaiter15.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }


            void TryInvokeContinuationWithIncrement()
            {
                if (Interlocked.Increment(ref completedCount) == ResultCount)
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

            public (T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                return (t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>)state;
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
                var self = (WhenAllPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

    }
}