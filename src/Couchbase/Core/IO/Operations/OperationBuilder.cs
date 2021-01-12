using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Utils;
using ByteConverter = Couchbase.Core.IO.Converters.ByteConverter;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Provides a forward-only stream for building operations which tracks the size of each segment
    /// as it's being built.
    /// </summary>
    internal sealed class OperationBuilder : Stream
    {
        private readonly MemoryStream _stream;

        private int _framingExtrasLength;
        private int _extrasLength;
        private int _keyLength;
        private int _bodyLength;

        private int _operationSpecStartPosition;
        private bool _operationSpecIsMutation;
        private int _operationSpecPathLength;
        private int _operationSpecFragmentLength;

        private bool _headerWritten;

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override long Length => _stream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _stream.Position;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Capacity of the underlying stream.
        /// </summary>
        public long Capacity => _stream.Capacity;

        /// <summary>
        /// The current segment being written.
        /// </summary>
        public OperationSegment CurrentSegment { get; private set; }

        /// <summary>
        /// Creates a new OperationBuilder.
        /// </summary>
        public OperationBuilder()
        {
            _stream = MemoryStreamFactory.GetMemoryStream();

            Reset();
        }

        /// <summary>
        /// Advance to another segment for writing. Segments may be skipped, but must be advanced in order.
        /// </summary>
        /// <param name="segment">New segment for subsequent writes.</param>
        /// <exception cref="InvalidEnumArgumentException"><paramref name="segment"/> is not a valid value.</exception>
        /// <exception cref="InvalidOperationException">Attempt to move the segment backwards, or the header has already been written.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceToSegment(OperationSegment segment)
        {
            if (segment < OperationSegment.FramingExtras || segment > OperationSegment.OperationSpecFragment)
            {
                ThrowHelper.ThrowInvalidEnumArgumentException(nameof(segment), (int) segment, typeof(OperationSegment));
            }

            if (segment < CurrentSegment)
            {
                ThrowHelper.ThrowInvalidOperationException("Segment cannot be moved backwards");
            }
            if (CurrentSegment <= OperationSegment.Body && segment > OperationSegment.Body)
            {
                ThrowHelper.ThrowInvalidOperationException("Operation specs must be started with BeginOperationSpec");
            }
            if (segment == OperationSegment.OperationSpecFragment && !_operationSpecIsMutation)
            {
                ThrowHelper.ThrowInvalidOperationException("This operation spec is not a mutation");
            }

            EnsureHeaderNotWritten();

            CurrentSegment = segment;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _stream.Flush();
        }

        /// <summary>
        /// Gets a block of memory containing the operation. <see cref="WriteHeader"/> must be called before this method.
        /// </summary>
        /// <returns>A block of memory containing the operation.</returns>
        /// <exception cref="InvalidOperationException">The header has not been written.</exception>
        /// <remarks>
        /// The memory block is only valid while the OperationBuilder exists. Once disposed, the memory
        /// should not be used.
        /// </remarks>
        public ReadOnlyMemory<byte> GetBuffer()
        {
            if (!_headerWritten)
            {
                ThrowHelper.ThrowInvalidOperationException("The header has not been written.");
            }

            return _stream.GetBuffer().AsMemory(0, (int) _stream.Length);
        }

        /// <inheritdoc />
        /// <summary>
        /// Not supported on <see cref="T:Couchbase.Core.IO.Operations.OperationBuilder" />.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        /// <summary>
        /// Not supported on <see cref="T:Couchbase.Core.IO.Operations.OperationBuilder" />.
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        /// <summary>
        /// Not supported on <see cref="T:Couchbase.Core.IO.Operations.OperationBuilder" />.
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureHeaderNotWritten();

            _stream.Write(buffer, offset, count);

            Advance(count);
        }

#if SPAN_SUPPORT
        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureHeaderNotWritten();

            _stream.Write(buffer);

            Advance(buffer.Length);
        }
#else
        /// <summary>
        /// Writes a span of bytes to the stream.
        /// </summary>
        /// <param name="buffer">Bytes to write.</param>
        public void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureHeaderNotWritten();

            if (buffer.Length == 0)
            {
                return;
            }

            var byteBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(byteBuffer);

                _stream.Write(byteBuffer, 0, buffer.Length);

                Advance(buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(byteBuffer);
            }
        }
#endif

        /// <summary>
        /// Writes the header of the operation.
        /// </summary>
        /// <param name="header"></param>
        /// <remarks>
        /// After the header is written, the stream cannot receive any other write operations.
        /// </remarks>
        public void WriteHeader(in OperationRequestHeader header)
        {
            EnsureHeaderNotWritten();

            if (CurrentSegment > OperationSegment.Body)
            {
                ThrowHelper.ThrowInvalidOperationException("An operation spec is still in progress.");
            }

            // Make sure we slice this span so the length is known to the JIT compiler.
            // This allows it to optimize away length checks for calls below.
            Span<byte> headerBytes = _stream.GetBuffer().AsSpan(0, OperationHeader.Length);
            headerBytes.Fill(0);

            if (_framingExtrasLength > 0)
            {
                headerBytes[HeaderOffsets.Magic] = (byte) Magic.AltRequest;
                headerBytes[HeaderOffsets.KeyLength] = (byte) _framingExtrasLength;
                headerBytes[HeaderOffsets.AltKeyLength] = (byte) _keyLength;
            }
            else
            {
                headerBytes[HeaderOffsets.Magic] = (byte) Magic.Request;
                ByteConverter.FromInt16((short) _keyLength, headerBytes.Slice(HeaderOffsets.KeyLength));
            }

            headerBytes[HeaderOffsets.Opcode] = (byte) header.OpCode;
            headerBytes[HeaderOffsets.Datatype] = (byte) header.DataType;
            headerBytes[HeaderOffsets.ExtrasLength] = (byte) _extrasLength;

            if (header.VBucketId.HasValue)
            {
                ByteConverter.FromInt16(header.VBucketId.GetValueOrDefault(), headerBytes.Slice(HeaderOffsets.VBucket));
            }

            var totalLength = _framingExtrasLength + _extrasLength + _keyLength + _bodyLength;
            ByteConverter.FromInt32(totalLength, headerBytes.Slice(HeaderOffsets.BodyLength));
            ByteConverter.FromUInt32(header.Opaque, headerBytes.Slice(HeaderOffsets.Opaque));
            ByteConverter.FromUInt64(header.Cas, headerBytes.Slice(HeaderOffsets.Cas));

            _headerWritten = true;
        }

        /// <summary>
        /// Begin a sub-document operation spec within the operation body.
        /// </summary>
        /// <param name="isMutation">This is a mutation operation which should have a fragment length.</param>
        /// <remarks>
        /// Each call to BeginOperationSpec should be followed by a call to <see cref="CompleteOperationSpec"/>
        /// once the path and fragment are written. <see cref="AdvanceToSegment"/> can be used to to move from
        /// writing the path to writing the fragment.
        /// </remarks>
        public void BeginOperationSpec(bool isMutation)
        {
            EnsureHeaderNotWritten();

            if (CurrentSegment < OperationSegment.Body)
            {
                AdvanceToSegment(OperationSegment.Body);
            }
            else if (CurrentSegment > OperationSegment.Body)
            {
                ThrowHelper.ThrowInvalidOperationException("Operation spec is already in progress");
            }

            var headerSize = isMutation ? 8 : 4;

            _operationSpecStartPosition = (int) _stream.Position;
            _stream.SetLength(_stream.Length + headerSize);
            _stream.Position += headerSize;

            _operationSpecIsMutation = isMutation;
            _operationSpecPathLength = 0;
            _operationSpecFragmentLength = 0;
            CurrentSegment = OperationSegment.OperationSpecPath;
        }

        public void Reset()
        {
            // Skip the bytes for the header, which will be written later once lengths are known.
            _stream.SetLength(OperationHeader.Length);
            _stream.Position = OperationHeader.Length;

            _framingExtrasLength = 0;
            _extrasLength = 0;
            _keyLength = 0;
            _bodyLength = 0;
            _headerWritten = false;
            CurrentSegment = OperationSegment.FramingExtras;
        }

        /// <summary>
        /// Completes an in progress operation spec.
        /// </summary>
        /// <param name="spec">The spec which was written.</param>
        /// <remarks>
        /// This method should be called after the path and fragment for each operation have been written,
        /// and before the next call to <see cref="BeginOperationSpec"/>.
        /// </remarks>
        public void CompleteOperationSpec(OperationSpec spec)
        {
            if (CurrentSegment < OperationSegment.Body)
            {
                ThrowHelper.ThrowInvalidOperationException("An operation spec is not in progress.");
            }

            Span<byte> buffer = stackalloc byte[_operationSpecIsMutation ? 8 : 4];
            buffer[0] = (byte) spec.OpCode;
            buffer[1] = (byte) spec.PathFlags;
            ByteConverter.FromUInt16((ushort) _operationSpecPathLength, buffer.Slice(2));

            if (_operationSpecIsMutation)
            {
                ByteConverter.FromUInt32((uint) _operationSpecFragmentLength, buffer.Slice(4));
            }

            _stream.Position = _operationSpecStartPosition;
            Write(buffer);
            _stream.Position = _stream.Length;

            CurrentSegment = OperationSegment.Body;
        }

        /// <summary>
        /// Replaces the body of the operation in the stream with a compressed body, if requirements are met.
        /// </summary>
        /// <param name="operationCompressor">The <see cref="IOperationCompressor"/>.</param>
        /// <returns>True if the body was compressed, otherwise false.</returns>
        public bool AttemptBodyCompression(IOperationCompressor operationCompressor)
        {
            if (_bodyLength <= 0)
            {
                // Fast short circuit for operations with no body..
                return false;
            }

            var bodyStart = OperationHeader.Length + _framingExtrasLength + _extrasLength + _keyLength;
            var body = _stream.GetBuffer().AsMemory(bodyStart, _bodyLength);

            using var compressed = operationCompressor.Compress(body);
            if (compressed == null)
            {
                return false;
            }

            var compressedSpan = compressed.Memory.Span;

            // Replace the body with the compressed body
            compressedSpan.CopyTo(body.Span);
            _stream.SetLength(bodyStart + compressedSpan.Length);
            _bodyLength = compressedSpan.Length;
            return true;
        }

        /// <summary>
        /// After writing to the stream, records the number of bytes written to the current segment.
        /// </summary>
        /// <param name="bytes">Number of bytes written.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance(int bytes)
        {
            switch (CurrentSegment)
            {
                case OperationSegment.FramingExtras:
                    _framingExtrasLength += bytes;
                    break;

                case OperationSegment.Extras:
                    _extrasLength += bytes;
                    break;

                case OperationSegment.Key:
                    _keyLength += bytes;
                    break;

                case OperationSegment.Body:
                    _bodyLength += bytes;
                    break;

                case OperationSegment.OperationSpecPath:
                    _bodyLength += bytes;
                    _operationSpecPathLength += bytes;
                    break;

                case OperationSegment.OperationSpecFragment:
                    _bodyLength += bytes;
                    _operationSpecFragmentLength += bytes;
                    break;
            }
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if the header has already been written.
        /// </summary>
        private void EnsureHeaderNotWritten()
        {
            if (_headerWritten)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot write data after the header has been written");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
        }
    }
}
