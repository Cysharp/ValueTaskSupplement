using System.Threading.Tasks;



namespace ValueTaskSupplement
{
    public static partial class ValueTaskEx
    {
        public static ValueTask FromTask(Task result)
            => new ValueTask(result);


        public static ValueTask<T> FromTask<T>(Task<T> result)
            => new ValueTask<T>(result);


        public static ValueTask AsValueTask(this Task result)
            => new ValueTask(result);


        public static ValueTask<T> AsValueTask<T>(this Task<T> result)
            => new ValueTask<T>(result);
    }
}