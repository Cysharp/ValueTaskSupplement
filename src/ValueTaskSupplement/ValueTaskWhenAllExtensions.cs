using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace ValueTaskSupplement
{
    public static class ValueTaskWhenAllExtensions
    {

        public static ValueTaskAwaiter<(T0, T1)> GetAwaiter<T0, T1>(this (ValueTask<T0> task0, ValueTask<T1> task1) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2)> GetAwaiter<T0, T1, T2>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3)> GetAwaiter<T0, T1, T2, T3>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4)> GetAwaiter<T0, T1, T2, T3, T4>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5)> GetAwaiter<T0, T1, T2, T3, T4, T5>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14, tasks.Item15).GetAwaiter();
        }

        public static ValueTaskAwaiter<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)> GetAwaiter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this (ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15) tasks)
        {
            return ValueTaskEx.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7, tasks.Item8, tasks.Item9, tasks.Item10, tasks.Item11, tasks.Item12, tasks.Item13, tasks.Item14, tasks.Item15, tasks.Item16).GetAwaiter();
        }
    }
}