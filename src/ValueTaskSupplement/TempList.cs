using System;
using System.Buffers;

namespace ValueTaskSupplement
{
    internal ref struct TempList<T>
    {
        int index;
        T[] array;

        public TempList(int initialCapacity)
        {
            this.array = ArrayPool<T>.Shared.Rent(initialCapacity);
            this.index = 0;
        }

        public void Add(T value)
        {
            if (array.Length <= index)
            {
                var newArray = ArrayPool<T>.Shared.Rent(index * 2);
                Array.Copy(array, newArray, index);
                ArrayPool<T>.Shared.Return(array, true);
                array = newArray;
            }

            array[index++] = value;
        }

        public ReadOnlySpan<T> AsSpan()
        {
            return new ReadOnlySpan<T>(array, 0, index);
        }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(array, true); // clear for de-reference all.
        }
    }
}
