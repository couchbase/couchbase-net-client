using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Utils;
using ByteConverter = Couchbase.Core.IO.Converters.ByteConverter;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Provides a forward-only stream for building operations which tracks the size of each segment
    /// as it's being built.
    /// </summary>
    internal sealed class OperationBuilder : Stream, IBufferWriter<byte>
    {
        private const int MinimumBufferSize = 16384;

        private byte[]? _buffer; // null indicates disposed

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

        private int _length;
        /// <inheritdoc />
        public override long Length => _length;

        private int _position;
        /// <inheritdoc />
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Capacity of the underlying stream.
        /// </summary>
        public int Capacity
        {
            get
            {
                EnsureNotDisposed();
                return _buffer.Length;
            }
        }

        /// <summary>
        /// The current segment being written.
        /// </summary>
        public OperationSegment CurrentSegment { get; private set; }

        /// <summary>
        /// Creates a new OperationBuilder.
        /// </summary>
        public OperationBuilder()
        {
            _buffer = ArrayPool<byte>.Shared.Rent(MinimumBufferSize);

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

            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            CurrentSegment = segment;
        }

        private void SetLength(int length)
        {
            Debug.Assert(_buffer is not null);

            if (length < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
            }

            EnsureCapacity(length);
            _length = length;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            EnsureNotDisposed();
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
            EnsureNotDisposed();
            if (!_headerWritten)
            {
                ThrowHelper.ThrowInvalidOperationException("The header has not been written.");
            }

            return _buffer.AsMemory(0, _length);
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
        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public
#if SPAN_SUPPORT
            override
#endif
            void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            buffer.CopyTo(GetSpan(buffer.Length));
            Advance(buffer.Length);
        }

        /// <inheritdoc />
        public override void WriteByte(byte value)
        {
            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            CheckAndResizeBuffer(1);
            _buffer[_position] = value;
            Advance(1);
        }

        /// <summary>
        /// Writes the header of the operation.
        /// </summary>
        /// <param name="header"></param>
        /// <remarks>
        /// After the header is written, the stream cannot receive any other write operations.
        /// </remarks>
        public void WriteHeader(in OperationRequestHeader header)
        {
            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            if (CurrentSegment > OperationSegment.Body)
            {
                ThrowHelper.ThrowInvalidOperationException("An operation spec is still in progress.");
            }

            // Make sure we slice this span so the length is known to the JIT compiler.
            // This may allow it to optimize away length checks for calls below.
            Span<byte> headerBytes = _buffer.AsSpan(0, OperationHeader.Length);

            if (_framingExtrasLength > 0)
            {
                headerBytes[HeaderOffsets.Magic] = (byte) Magic.AltRequest;
                headerBytes[HeaderOffsets.KeyLength] = (byte) _framingExtrasLength;
                headerBytes[HeaderOffsets.AltKeyLength] = (byte) _keyLength;
            }
            else
            {
                headerBytes[HeaderOffsets.Magic] = (byte) Magic.ClientRequest;
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
            ByteConverter.FromUInt32(header.Opaque, headerBytes.Slice(HeaderOffsets.Opaque), false);
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
            EnsureNotDisposed();
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

            _operationSpecStartPosition = _position;
            Advance(headerSize);

            _operationSpecIsMutation = isMutation;
            _operationSpecPathLength = 0;
            _operationSpecFragmentLength = 0;
            CurrentSegment = OperationSegment.OperationSpecPath;
        }

        /// <summary>
        /// Reset the builder for reuse.
        /// </summary>
        public void Reset()
        {
            EnsureNotDisposed();

            // Clear the buffer to avoid leaks between operations
            // For new OperationBuilder objects _length will be zero here so the clear is a noop
            _buffer.AsSpan(0, _length).Clear();

            // Skip the bytes for the header, which will be written later once lengths are known.
            _length = OperationHeader.Length;
            _position = OperationHeader.Length;

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
            EnsureNotDisposed();
            if (CurrentSegment < OperationSegment.Body)
            {
                ThrowHelper.ThrowInvalidOperationException("An operation spec is not in progress.");
            }

            // Temporarily back up to the header for the operation spec
            _position = _operationSpecStartPosition;

            var headerLength = _operationSpecIsMutation ? 8 : 4;
            var buffer = GetSpan(headerLength);
            buffer[0] = (byte) spec.OpCode;
            buffer[1] = (byte) spec.PathFlags;
            ByteConverter.FromUInt16((ushort) _operationSpecPathLength, buffer.Slice(2));

            if (_operationSpecIsMutation)
            {
                ByteConverter.FromUInt32((uint) _operationSpecFragmentLength, buffer.Slice(4));
            }

            // Move back to the end of the operation spec
            _position = _length;

            CurrentSegment = OperationSegment.Body;
        }

        /// <summary>
        /// Replaces the body of the operation in the stream with a compressed body, if requirements are met.
        /// </summary>
        /// <param name="operationCompressor">The <see cref="IOperationCompressor"/>.</param>
        /// <param name="parentSpan">If compression is attempted, the parent span for tracing.</param>
        /// <returns>True if the body was compressed, otherwise false.</returns>
        public bool AttemptBodyCompression(IOperationCompressor operationCompressor, IRequestSpan parentSpan)
        {
            EnsureNotDisposed();
            if (_bodyLength <= 0)
            {
                // Fast short circuit for operations with no body..
                return false;
            }

            var bodyStart = OperationHeader.Length + _framingExtrasLength + _extrasLength + _keyLength;
            var body = _buffer.AsMemory(bodyStart, _bodyLength);

            using var compressed = operationCompressor.Compress(body, parentSpan);
            if (compressed == null)
            {
                return false;
            }

            var compressedSpan = compressed.Memory.Span;

            // Replace the body with the compressed body
            compressedSpan.CopyTo(body.Span);
            SetLength(bodyStart + compressedSpan.Length);
            _bodyLength = compressedSpan.Length;
            return true;
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if the header has already been written.
        /// </summary>
        [MemberNotNull(nameof(_buffer))]
        private void EnsureNotDisposed()
        {
            if (_buffer is null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(OperationBuilder));
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
            if (_buffer == null)
            {
                return;
            }

            _buffer.AsSpan(0, _length).Clear();
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }

        #region IBufferWriter

        /// <inheritdoc />
        public void Advance(int count)
        {
            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
            }
            if (_position > _buffer.Length - count)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot advance past the end of the buffer.");
            }

            _position += count;
            if (_position > _length)
            {
                _length = _position;
            }

            switch (CurrentSegment)
            {
                case OperationSegment.FramingExtras:
                    _framingExtrasLength += count;
                    break;

                case OperationSegment.Extras:
                    _extrasLength += count;
                    break;

                case OperationSegment.Key:
                    _keyLength += count;
                    break;

                case OperationSegment.Body:
                    _bodyLength += count;
                    break;

                case OperationSegment.OperationSpecPath:
                    _bodyLength += count;
                    _operationSpecPathLength += count;
                    break;

                case OperationSegment.OperationSpecFragment:
                    _bodyLength += count;
                    _operationSpecFragmentLength += count;
                    break;
            }
        }

        /// <inheritdoc />
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsMemory(_position);
        }

        /// <inheritdoc />
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureNotDisposed();
            EnsureHeaderNotWritten();

            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsSpan(_position);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(sizeHint));
            }
            if (sizeHint == 0)
            {
                sizeHint = MinimumBufferSize;
            }

            EnsureCapacity(_position + sizeHint);
        }

        // Internal for unit testing
        internal void EnsureCapacity(int capacity)
        {
            Debug.Assert(_buffer is not null);

            if (_buffer!.Length < capacity)
            {
                var oldBuffer = _buffer;

                _buffer = ArrayPool<byte>.Shared.Rent(capacity);

                Debug.Assert(oldBuffer.Length >= _length);
                Debug.Assert(_buffer.Length >= _length);

                // Copy the previous buffer to the new buffer and clear the portion
                // of the buffer we've used before returning it to the pool
                var previousBuffer = oldBuffer.AsSpan(0, _length);
                previousBuffer.CopyTo(_buffer);
                previousBuffer.Clear();

                ArrayPool<byte>.Shared.Return(oldBuffer);
            }

            Debug.Assert(_buffer.Length >= capacity);
        }

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
