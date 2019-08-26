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
        public static ValueTask WhenAll(ValueTask task0, ValueTask task1)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise2(task0, task1), 0);
        }

        class WhenAllPromise2 : IValueTaskSource
        {
            const int ResultCount = 2;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise2(ValueTask task0, ValueTask task1)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise2>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise2>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise3(task0, task1, task2), 0);
        }

        class WhenAllPromise3 : IValueTaskSource
        {
            const int ResultCount = 3;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise3(ValueTask task0, ValueTask task1, ValueTask task2)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise3>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise3>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise4(task0, task1, task2, task3), 0);
        }

        class WhenAllPromise4 : IValueTaskSource
        {
            const int ResultCount = 4;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise4(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise4>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise4>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise5(task0, task1, task2, task3, task4), 0);
        }

        class WhenAllPromise5 : IValueTaskSource
        {
            const int ResultCount = 5;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise5(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise5>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise5>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise6(task0, task1, task2, task3, task4, task5), 0);
        }

        class WhenAllPromise6 : IValueTaskSource
        {
            const int ResultCount = 6;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise6(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise6>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise6>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise7(task0, task1, task2, task3, task4, task5, task6), 0);
        }

        class WhenAllPromise7 : IValueTaskSource
        {
            const int ResultCount = 7;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise7(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise7>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise7>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise8(task0, task1, task2, task3, task4, task5, task6, task7), 0);
        }

        class WhenAllPromise8 : IValueTaskSource
        {
            const int ResultCount = 8;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise8(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise8>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise8>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise9(task0, task1, task2, task3, task4, task5, task6, task7, task8), 0);
        }

        class WhenAllPromise9 : IValueTaskSource
        {
            const int ResultCount = 9;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise9(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise9>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise9>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise10(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9), 0);
        }

        class WhenAllPromise10 : IValueTaskSource
        {
            const int ResultCount = 10;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise10(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise10>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise10>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise11(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10), 0);
        }

        class WhenAllPromise11 : IValueTaskSource
        {
            const int ResultCount = 11;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;
            ValueTaskAwaiter awaiter10;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise11(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation10);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation10()
            {
                try
                {
                    awaiter10.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise11>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise11>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise12(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11), 0);
        }

        class WhenAllPromise12 : IValueTaskSource
        {
            const int ResultCount = 12;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;
            ValueTaskAwaiter awaiter10;
            ValueTaskAwaiter awaiter11;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise12(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation11);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation10()
            {
                try
                {
                    awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation11()
            {
                try
                {
                    awaiter11.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise12>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise12>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise13(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12), 0);
        }

        class WhenAllPromise13 : IValueTaskSource
        {
            const int ResultCount = 13;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;
            ValueTaskAwaiter awaiter10;
            ValueTaskAwaiter awaiter11;
            ValueTaskAwaiter awaiter12;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise13(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation12);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation10()
            {
                try
                {
                    awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation11()
            {
                try
                {
                    awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation12()
            {
                try
                {
                    awaiter12.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise13>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise13>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully && task13.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise14(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13), 0);
        }

        class WhenAllPromise14 : IValueTaskSource
        {
            const int ResultCount = 14;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;
            ValueTaskAwaiter awaiter10;
            ValueTaskAwaiter awaiter11;
            ValueTaskAwaiter awaiter12;
            ValueTaskAwaiter awaiter13;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise14(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation12);
                    }
                }
                {
                    var awaiter = task13.GetAwaiter();
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
                        awaiter13 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation13);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation10()
            {
                try
                {
                    awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation11()
            {
                try
                {
                    awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation12()
            {
                try
                {
                    awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation13()
            {
                try
                {
                    awaiter13.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise14>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise14>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully && task13.IsCompletedSuccessfully && task14.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise15(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14), 0);
        }

        class WhenAllPromise15 : IValueTaskSource
        {
            const int ResultCount = 15;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;
            ValueTaskAwaiter awaiter10;
            ValueTaskAwaiter awaiter11;
            ValueTaskAwaiter awaiter12;
            ValueTaskAwaiter awaiter13;
            ValueTaskAwaiter awaiter14;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise15(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation12);
                    }
                }
                {
                    var awaiter = task13.GetAwaiter();
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
                        awaiter13 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation13);
                    }
                }
                {
                    var awaiter = task14.GetAwaiter();
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
                        awaiter14 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation14);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation10()
            {
                try
                {
                    awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation11()
            {
                try
                {
                    awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation12()
            {
                try
                {
                    awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation13()
            {
                try
                {
                    awaiter13.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation14()
            {
                try
                {
                    awaiter14.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise15>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise15>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask WhenAll(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14, ValueTask task15)
        {
            if (task0.IsCompletedSuccessfully && task1.IsCompletedSuccessfully && task2.IsCompletedSuccessfully && task3.IsCompletedSuccessfully && task4.IsCompletedSuccessfully && task5.IsCompletedSuccessfully && task6.IsCompletedSuccessfully && task7.IsCompletedSuccessfully && task8.IsCompletedSuccessfully && task9.IsCompletedSuccessfully && task10.IsCompletedSuccessfully && task11.IsCompletedSuccessfully && task12.IsCompletedSuccessfully && task13.IsCompletedSuccessfully && task14.IsCompletedSuccessfully && task15.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(new WhenAllPromise16(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14, task15), 0);
        }

        class WhenAllPromise16 : IValueTaskSource
        {
            const int ResultCount = 16;
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;
            ValueTaskAwaiter awaiter6;
            ValueTaskAwaiter awaiter7;
            ValueTaskAwaiter awaiter8;
            ValueTaskAwaiter awaiter9;
            ValueTaskAwaiter awaiter10;
            ValueTaskAwaiter awaiter11;
            ValueTaskAwaiter awaiter12;
            ValueTaskAwaiter awaiter13;
            ValueTaskAwaiter awaiter14;
            ValueTaskAwaiter awaiter15;

            int completedCount = 0;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAllPromise16(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14, ValueTask task15)
            {
                {
                    var awaiter = task0.GetAwaiter();
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
                        awaiter0 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation0);
                    }
                }
                {
                    var awaiter = task1.GetAwaiter();
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
                        awaiter1 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation1);
                    }
                }
                {
                    var awaiter = task2.GetAwaiter();
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
                        awaiter2 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation2);
                    }
                }
                {
                    var awaiter = task3.GetAwaiter();
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
                        awaiter3 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation3);
                    }
                }
                {
                    var awaiter = task4.GetAwaiter();
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
                        awaiter4 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation4);
                    }
                }
                {
                    var awaiter = task5.GetAwaiter();
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
                        awaiter5 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation5);
                    }
                }
                {
                    var awaiter = task6.GetAwaiter();
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
                        awaiter6 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation6);
                    }
                }
                {
                    var awaiter = task7.GetAwaiter();
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
                        awaiter7 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation7);
                    }
                }
                {
                    var awaiter = task8.GetAwaiter();
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
                        awaiter8 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation8);
                    }
                }
                {
                    var awaiter = task9.GetAwaiter();
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
                        awaiter9 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation9);
                    }
                }
                {
                    var awaiter = task10.GetAwaiter();
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
                        awaiter10 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation10);
                    }
                }
                {
                    var awaiter = task11.GetAwaiter();
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
                        awaiter11 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation11);
                    }
                }
                {
                    var awaiter = task12.GetAwaiter();
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
                        awaiter12 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation12);
                    }
                }
                {
                    var awaiter = task13.GetAwaiter();
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
                        awaiter13 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation13);
                    }
                }
                {
                    var awaiter = task14.GetAwaiter();
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
                        awaiter14 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation14);
                    }
                }
                {
                    var awaiter = task15.GetAwaiter();
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
                        awaiter15 = awaiter;
                        awaiter.UnsafeOnCompleted(Continuation15);
                    }
                }
            }

            void Continuation0()
            {
                try
                {
                    awaiter0.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation1()
            {
                try
                {
                    awaiter1.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation2()
            {
                try
                {
                    awaiter2.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation3()
            {
                try
                {
                    awaiter3.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation4()
            {
                try
                {
                    awaiter4.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation5()
            {
                try
                {
                    awaiter5.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation6()
            {
                try
                {
                    awaiter6.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation7()
            {
                try
                {
                    awaiter7.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation8()
            {
                try
                {
                    awaiter8.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation9()
            {
                try
                {
                    awaiter9.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation10()
            {
                try
                {
                    awaiter10.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation11()
            {
                try
                {
                    awaiter11.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation12()
            {
                try
                {
                    awaiter12.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation13()
            {
                try
                {
                    awaiter13.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation14()
            {
                try
                {
                    awaiter14.GetResult();
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                    TryInvokeContinuation();
                    return;
                }
                TryInvokeContinuationWithIncrement();
            }

            void Continuation15()
            {
                try
                {
                    awaiter15.GetResult();
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
                return (completedCount == ResultCount) ? ValueTaskSourceStatus.Succeeded
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
                var t = (Tuple<Action<object>, WhenAllPromise16>)state;
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
                var t = (Tuple<Action<object>, WhenAllPromise16>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

    }
}