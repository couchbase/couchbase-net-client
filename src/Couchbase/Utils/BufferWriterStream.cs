using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace Couchbase.Utils
{
    /// <summary>
    /// Wraps an <see cref="IBufferWriter{T}"/> in a Stream.
    /// </summary>
    internal sealed class BufferWriterStream(IBufferWriter<byte> writer) : Stream
    {
        private const string ReadNotSupportedMessage = $"{nameof(BufferWriterStream)} is write only.";
        private const string SeekNotSupportedMessage = $"{nameof(BufferWriterStream)} is not seekable.";

        private bool _disposed;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length
        {
            get
            {
                ThrowHelper.ThrowNotSupportedException(SeekNotSupportedMessage);
                return default;
            }
        }

        public override long Position
        {
            get
            {
                ThrowHelper.ThrowNotSupportedException(SeekNotSupportedMessage);
                return default;
            }
            set => ThrowHelper.ThrowNotSupportedException(SeekNotSupportedMessage);
        }

        public override void Flush()
        {
            EnsureNotDisposed();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowHelper.ThrowNotSupportedException(ReadNotSupportedMessage);
            return default;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowHelper.ThrowNotSupportedException(SeekNotSupportedMessage);
            return default;
        }

        public override void SetLength(long value)
        {
            ThrowHelper.ThrowNotSupportedException(SeekNotSupportedMessage);
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public
#if SPAN_SUPPORT
            override
#endif
            void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureNotDisposed();

            if (buffer.Length == 0)
            {
                return;
            }

            writer.Write(buffer);
        }

#if SPAN_SUPPORT
        public override void WriteByte(byte value)
        {
            Write(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        }
#endif

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(BufferWriterStream));
            }
        }
    }
}
