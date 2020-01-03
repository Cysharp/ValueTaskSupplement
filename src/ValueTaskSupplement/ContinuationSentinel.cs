using System;

namespace ValueTaskSupplement
{
    internal static class ContinuationSentinel
    {
        public static readonly Action<object?> AvailableContinuation = _ => { };
        public static readonly Action<object?> CompletedContinuation = _ => { };
    }
}
