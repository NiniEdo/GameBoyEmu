using System.Collections.Generic;

namespace GameBoyEmu.Utils
{
    public class FixedSizeQueue<T> : Queue<T>
    {
        public int MaxSize { get; private set; }

        public FixedSizeQueue(int maxSize)
        {
            MaxSize = maxSize;
        }

        public new void Enqueue(T item)
        {
            if (Count >= MaxSize)
            {
                throw new IndexOutOfRangeException("Index of queue out of range");
            }
            base.Enqueue(item);
        }
    }
}
