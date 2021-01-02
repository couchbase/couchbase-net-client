using System;
using System.Buffers;
using Couchbase.Utils;

namespace Couchbase.LoadTests.Helpers
{
    internal class FakeMemoryOwner<T> : IMemoryOwner<T>
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

        public static implicit operator SlicedMemoryOwner<T>(FakeMemoryOwner<T> fakeMemoryOwner) =>
            new SlicedMemoryOwner<T>(fakeMemoryOwner);
    }
}
