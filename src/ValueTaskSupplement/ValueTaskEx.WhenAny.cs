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
        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1)> WhenAny<T0, T1>(ValueTask<T0> task0, ValueTask<T1> task1)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1)>(new WhenAnyPromise<T0, T1>(task0, task1), 0);
        }

        class WhenAnyPromise<T0, T1> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1)>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1);
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
                var self = (WhenAnyPromise<T0, T1>)state;
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
                var self = (WhenAnyPromise<T0, T1>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2)> WhenAny<T0, T1, T2>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2)>(new WhenAnyPromise<T0, T1, T2>(task0, task1, task2), 0);
        }

        class WhenAnyPromise<T0, T1, T2> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2)>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            T0 t0 = default!;
            T1 t1 = default!;
            T2 t2 = default!;
            ValueTaskAwaiter<T0> awaiter0;
            ValueTaskAwaiter<T1> awaiter1;
            ValueTaskAwaiter<T2> awaiter2;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2);
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
                var self = (WhenAnyPromise<T0, T1, T2>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3)> WhenAny<T0, T1, T2, T3>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3)>(new WhenAnyPromise<T0, T1, T2, T3>(task0, task1, task2, task3), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4)> WhenAny<T0, T1, T2, T3, T4>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4)>(new WhenAnyPromise<T0, T1, T2, T3, T4>(task0, task1, task2, task3, task4), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5)> WhenAny<T0, T1, T2, T3, T4, T5>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5>(task0, task1, task2, task3, task4, task5), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6)> WhenAny<T0, T1, T2, T3, T4, T5, T6>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6>(task0, task1, task2, task3, task4, task5, task6), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7>(task0, task1, task2, task3, task4, task5, task6, task7), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8>(task0, task1, task2, task3, task4, task5, task6, task7, task8), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                            TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                            TryInvokeContinuationWithIncrement(10);
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
                            TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                            TryInvokeContinuationWithIncrement(10);
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
                            TryInvokeContinuationWithIncrement(11);
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
                            TryInvokeContinuationWithIncrement(12);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                            TryInvokeContinuationWithIncrement(10);
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
                            TryInvokeContinuationWithIncrement(11);
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
                            TryInvokeContinuationWithIncrement(12);
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
                            TryInvokeContinuationWithIncrement(13);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
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
                TryInvokeContinuationWithIncrement(13);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                            TryInvokeContinuationWithIncrement(10);
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
                            TryInvokeContinuationWithIncrement(11);
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
                            TryInvokeContinuationWithIncrement(12);
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
                            TryInvokeContinuationWithIncrement(13);
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
                            TryInvokeContinuationWithIncrement(14);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
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
                TryInvokeContinuationWithIncrement(13);
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
                TryInvokeContinuationWithIncrement(14);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

        public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14, T15 result15)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15)
        {
            return new ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14, T15 result15)>(new WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14, task15), 0);
        }

        class WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> : IValueTaskSource<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14, T15 result15)>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo? exception;
            Action<object?> continuation = ContinuationSentinel.AvailableContinuation;
            Action<object?>? invokeContinuation;
            object? state;
            SynchronizationContext? syncContext;
            ExecutionContext? execContext;

            public WhenAnyPromise(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            t0 = awaiter.GetResult();
                            TryInvokeContinuationWithIncrement(0);
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
                            TryInvokeContinuationWithIncrement(1);
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
                            TryInvokeContinuationWithIncrement(2);
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
                            TryInvokeContinuationWithIncrement(3);
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
                            TryInvokeContinuationWithIncrement(4);
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
                            TryInvokeContinuationWithIncrement(5);
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
                            TryInvokeContinuationWithIncrement(6);
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
                            TryInvokeContinuationWithIncrement(7);
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
                            TryInvokeContinuationWithIncrement(8);
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
                            TryInvokeContinuationWithIncrement(9);
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
                            TryInvokeContinuationWithIncrement(10);
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
                            TryInvokeContinuationWithIncrement(11);
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
                            TryInvokeContinuationWithIncrement(12);
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
                            TryInvokeContinuationWithIncrement(13);
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
                            TryInvokeContinuationWithIncrement(14);
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
                            TryInvokeContinuationWithIncrement(15);
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
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
                TryInvokeContinuationWithIncrement(13);
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
                TryInvokeContinuationWithIncrement(14);
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
                TryInvokeContinuationWithIncrement(15);
            }


            void TryInvokeContinuationWithIncrement(int index)
            {
                if (Interlocked.Increment(ref completedCount) == 1)
                {
                    Volatile.Write(ref winArgumentIndex, index);
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

            public (int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14, T15 result15) GetResult(short token)
            {
                if (exception != null)
                {
                    exception.Throw();
                }
                var i = this.winArgumentIndex;
                return (winArgumentIndex, t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15);
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>)state;
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
                var self = (WhenAnyPromise<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>)state;
                var invokeContinuation = self.invokeContinuation!;
                var invokeState = self.state;
                self.invokeContinuation = null;
                self.state = null;
                invokeContinuation(invokeState);
            }
        }

    }
}