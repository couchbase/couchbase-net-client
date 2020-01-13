using System;
using System.ComponentModel;
using System.IO;

namespace Couchbase.Utils
{
    internal class MemoryReaderStream : Stream
    {
        private readonly ReadOnlyMemory<byte> _buffer;
        private int _position;
        private bool _disposed;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _buffer.Length;

        public override long Position
        {
            get => _position;
            set
            {
                EnsureNotDisposed();

                if (value < 0 || value > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = (int) value;
            }
        }

        public MemoryReaderStream(ReadOnlyMemory<byte> buffer)
        {
            _buffer = buffer;
        }

        public override void Flush()
        {
            EnsureNotDisposed();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureNotDisposed();

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (offset + count > buffer.Length)
            {
                throw new ArgumentException("Insufficient buffer", nameof(count));
            }

            if (_buffer.Length - _position < count)
            {
                count = Math.Max(_buffer.Length - _position, 0);
            }

            if (count > 0)
            {
                _buffer.Span.Slice(_position, count).CopyTo(buffer.AsSpan(offset, count));

                _position += count;
            }

            return count;
        }

#if NETCOREAPP2_1 || NETSTANDARD2_1

        public override int Read(Span<byte> buffer)
        {
            EnsureNotDisposed();

            var count = buffer.Length;
            if (_buffer.Length - _position < count)
            {
                count = Math.Max(_buffer.Length - _position, 0);
            }

            if (count > 0)
            {
                _buffer.Span.Slice(_position, count).CopyTo(buffer);

                _position += count;
            }

            return count;
        }

#endif

        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureNotDisposed();

            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                default:
                    throw new InvalidEnumArgumentException("Invalid origin", (int) origin, typeof(SeekOrigin));
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MemoryReaderStream));
            }
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
