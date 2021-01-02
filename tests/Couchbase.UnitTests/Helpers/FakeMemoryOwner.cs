using System;
using System.Buffers;
using Couchbase.Utils;

namespace Couchbase.UnitTests.Helpers
{
    internal class FakeMemoryOwner<T> : IMemoryOwner<T>
    {
        private readonly Memory<T> _memory;

        public FakeMemoryOwner(Memory<T> memory)
        {
            _memory = memory;
        }

        public Memory<T> Memory
        {
            get
            {
                if (Disposed)
                {
                    throw new ObjectDisposedException(nameof(FakeMemoryOwner<T>));
                }

                return _memory;
            }
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }

        public static implicit operator SlicedMemoryOwner<T>(FakeMemoryOwner<T> fakeMemoryOwner) =>
            new SlicedMemoryOwner<T>(fakeMemoryOwner);
    }
}
