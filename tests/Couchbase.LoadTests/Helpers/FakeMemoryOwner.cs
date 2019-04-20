using System;
using System.Buffers;

namespace Couchbase.LoadTests.Helpers
{
    public class FakeMemoryOwner<T> : IMemoryOwner<T>
    {
        private T[] _array;

        public FakeMemoryOwner(T[] array)
        {
            _array = array;
        }

        public Memory<T> Memory
        {
            get
            {
                if (Disposed)
                {
                    throw new ObjectDisposedException(nameof(FakeMemoryOwner<T>));
                }

                return _array.AsMemory();
            }
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            _array = null;
            Disposed = true;
        }
    }
}
