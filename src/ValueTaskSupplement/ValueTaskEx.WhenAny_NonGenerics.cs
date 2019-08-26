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
        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1)
        {
            return new ValueTask<int>(new WhenAnyPromise2(task0, task1), 0);
        }

        class WhenAnyPromise2 : IValueTaskSource<int>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise2(ValueTask task0, ValueTask task1)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                var t = (Tuple<Action<object>, WhenAnyPromise2>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise2>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2)
        {
            return new ValueTask<int>(new WhenAnyPromise3(task0, task1, task2), 0);
        }

        class WhenAnyPromise3 : IValueTaskSource<int>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise3(ValueTask task0, ValueTask task1, ValueTask task2)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                var t = (Tuple<Action<object>, WhenAnyPromise3>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise3>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3)
        {
            return new ValueTask<int>(new WhenAnyPromise4(task0, task1, task2, task3), 0);
        }

        class WhenAnyPromise4 : IValueTaskSource<int>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise4(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                var t = (Tuple<Action<object>, WhenAnyPromise4>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise4>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            return new ValueTask<int>(new WhenAnyPromise5(task0, task1, task2, task3, task4), 0);
        }

        class WhenAnyPromise5 : IValueTaskSource<int>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise5(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                var t = (Tuple<Action<object>, WhenAnyPromise5>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise5>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            return new ValueTask<int>(new WhenAnyPromise6(task0, task1, task2, task3, task4, task5), 0);
        }

        class WhenAnyPromise6 : IValueTaskSource<int>
        {
            static readonly ContextCallback execContextCallback = ExecutionContextCallback;
            static readonly SendOrPostCallback syncContextCallback = SynchronizationContextCallback;

            ValueTaskAwaiter awaiter0;
            ValueTaskAwaiter awaiter1;
            ValueTaskAwaiter awaiter2;
            ValueTaskAwaiter awaiter3;
            ValueTaskAwaiter awaiter4;
            ValueTaskAwaiter awaiter5;

            int completedCount = 0;
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise6(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                var t = (Tuple<Action<object>, WhenAnyPromise6>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise6>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6)
        {
            return new ValueTask<int>(new WhenAnyPromise7(task0, task1, task2, task3, task4, task5, task6), 0);
        }

        class WhenAnyPromise7 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise7(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                var t = (Tuple<Action<object>, WhenAnyPromise7>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise7>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7)
        {
            return new ValueTask<int>(new WhenAnyPromise8(task0, task1, task2, task3, task4, task5, task6, task7), 0);
        }

        class WhenAnyPromise8 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise8(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                var t = (Tuple<Action<object>, WhenAnyPromise8>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise8>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8)
        {
            return new ValueTask<int>(new WhenAnyPromise9(task0, task1, task2, task3, task4, task5, task6, task7, task8), 0);
        }

        class WhenAnyPromise9 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise9(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                var t = (Tuple<Action<object>, WhenAnyPromise9>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise9>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9)
        {
            return new ValueTask<int>(new WhenAnyPromise10(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9), 0);
        }

        class WhenAnyPromise10 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise10(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                var t = (Tuple<Action<object>, WhenAnyPromise10>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise10>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10)
        {
            return new ValueTask<int>(new WhenAnyPromise11(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10), 0);
        }

        class WhenAnyPromise11 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise11(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                var t = (Tuple<Action<object>, WhenAnyPromise11>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise11>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11)
        {
            return new ValueTask<int>(new WhenAnyPromise12(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11), 0);
        }

        class WhenAnyPromise12 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise12(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                var t = (Tuple<Action<object>, WhenAnyPromise12>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise12>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12)
        {
            return new ValueTask<int>(new WhenAnyPromise13(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12), 0);
        }

        class WhenAnyPromise13 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise13(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                var t = (Tuple<Action<object>, WhenAnyPromise13>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise13>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13)
        {
            return new ValueTask<int>(new WhenAnyPromise14(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13), 0);
        }

        class WhenAnyPromise14 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise14(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
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
                var t = (Tuple<Action<object>, WhenAnyPromise14>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise14>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14)
        {
            return new ValueTask<int>(new WhenAnyPromise15(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14), 0);
        }

        class WhenAnyPromise15 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise15(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
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
                TryInvokeContinuationWithIncrement(13);
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
                var t = (Tuple<Action<object>, WhenAnyPromise15>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise15>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

        public static ValueTask<int> WhenAny(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14, ValueTask task15)
        {
            return new ValueTask<int>(new WhenAnyPromise16(task0, task1, task2, task3, task4, task5, task6, task7, task8, task9, task10, task11, task12, task13, task14, task15), 0);
        }

        class WhenAnyPromise16 : IValueTaskSource<int>
        {
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
            int winArgumentIndex = -1;
            ExceptionDispatchInfo exception;
            Action<object> continuation = ContinuationSentinel.AvailableContinuation;
            object state;
            SynchronizationContext syncContext;
            ExecutionContext execContext;

            public WhenAnyPromise16(ValueTask task0, ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8, ValueTask task9, ValueTask task10, ValueTask task11, ValueTask task12, ValueTask task13, ValueTask task14, ValueTask task15)
            {
                {
                    var awaiter = task0.GetAwaiter();
                    if (awaiter.IsCompleted)
                    {
                        try
                        {
                            awaiter.GetResult();
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
                TryInvokeContinuationWithIncrement(0);
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
                TryInvokeContinuationWithIncrement(1);
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
                TryInvokeContinuationWithIncrement(2);
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
                TryInvokeContinuationWithIncrement(3);
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
                TryInvokeContinuationWithIncrement(4);
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
                TryInvokeContinuationWithIncrement(5);
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
                TryInvokeContinuationWithIncrement(6);
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
                TryInvokeContinuationWithIncrement(7);
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
                TryInvokeContinuationWithIncrement(8);
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
                TryInvokeContinuationWithIncrement(9);
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
                TryInvokeContinuationWithIncrement(10);
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
                TryInvokeContinuationWithIncrement(11);
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
                TryInvokeContinuationWithIncrement(12);
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
                TryInvokeContinuationWithIncrement(13);
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
                TryInvokeContinuationWithIncrement(14);
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
                var t = (Tuple<Action<object>, WhenAnyPromise16>)state;
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
                var t = (Tuple<Action<object>, WhenAnyPromise16>)state;
                var self = t.Item2;
                var invokeState = self.state;
                self.state = null;
                t.Item1.Invoke(invokeState);
            }
        }

    }
}