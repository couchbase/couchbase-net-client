using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.SubDocument;
using Microsoft.IO;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Provides a forward-only stream for building operations which tracks the size of each segment
    /// as it's being built.
    /// </summary>
    internal class OperationBuilder : Stream
    {
        private readonly IByteConverter _converter;
        private readonly RecyclableMemoryStream _stream;

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
        /// The current segment being written.
        /// </summary>
        public OperationSegment CurrentSegment { get; private set; }

        /// <summary>
        /// Creates a new OperationBuilder.
        /// </summary>
        /// <param name="converter">The <see cref="IByteConverter"/> to use when writing the header.</param>
        public OperationBuilder(IByteConverter converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _stream = MemoryStreamFactory.GetMemoryStream();

            // Skip the bytes for the header, which will be written later once lengths are known.
            _stream.SetLength(OperationHeader.Length);
            _stream.Position = OperationHeader.Length;

            CurrentSegment = OperationSegment.FramingExtras;
        }

        /// <summary>
        /// Advance to another segment for writing. Segments may be skipped, but must be advanced in order.
        /// </summary>
        /// <param name="segment">New segment for subsequent writes.</param>
        /// <exception cref="InvalidEnumArgumentException"><paramref name="segment"/> is not a valid value.</exception>
        /// <exception cref="InvalidOperationException">Attempt to move the segment backwards, or the header has already been written.</exception>
        public void AdvanceToSegment(OperationSegment segment)
        {
            if (!Enum.IsDefined(typeof(OperationSegment), segment))
            {
                throw new InvalidEnumArgumentException(nameof(segment), (int) segment, typeof(OperationSegment));
            }

            if (segment < CurrentSegment)
            {
                throw new InvalidOperationException("Segment cannot be moved backwards");
            }
            if (CurrentSegment <= OperationSegment.Body && segment > OperationSegment.Body)
            {
                throw new InvalidOperationException("Operation specs must be started with BeginOperationSpec");
            }
            if (segment == OperationSegment.OperationSpecFragment && !_operationSpecIsMutation)
            {
                throw new InvalidOperationException("This operation spec is not a mutation");
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
                throw new InvalidOperationException("The header has not been written.");
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

#if NETCOREAPP2_1
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
                Write(byteBuffer, 0, buffer.Length);
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
        public void WriteHeader(OperationRequestHeader header)
        {
            EnsureHeaderNotWritten();

            if (CurrentSegment > OperationSegment.Body)
            {
                throw new InvalidOperationException("An operation spec is still in progress.");
            }

            Span<byte> headerBytes = stackalloc byte[OperationHeader.Length];
            headerBytes.Fill(0);

            if (_framingExtrasLength > 0)
            {
                _converter.FromByte((byte) Magic.AltRequest, headerBytes.Slice(HeaderOffsets.Magic));
                _converter.FromByte((byte) _framingExtrasLength, headerBytes.Slice(HeaderOffsets.KeyLength));
                _converter.FromByte((byte) _keyLength, headerBytes.Slice(HeaderOffsets.AltKeyLength));
            }
            else
            {
                _converter.FromByte((byte) Magic.Request, headerBytes.Slice(HeaderOffsets.Magic));
                _converter.FromInt16((short) _keyLength, headerBytes.Slice(HeaderOffsets.KeyLength));
            }

            _converter.FromByte((byte)header.OpCode, headerBytes.Slice(HeaderOffsets.Opcode));
            _converter.FromByte((byte)_extrasLength, headerBytes.Slice(HeaderOffsets.ExtrasLength));

            if (header.VBucketId.HasValue)
            {
                _converter.FromInt16(header.VBucketId.Value, headerBytes.Slice(HeaderOffsets.VBucket));
            }

            var totalLength = _framingExtrasLength + _extrasLength + _keyLength + _bodyLength;
            _converter.FromInt32(totalLength, headerBytes.Slice(HeaderOffsets.BodyLength));
            _converter.FromUInt32(header.Opaque, headerBytes.Slice(HeaderOffsets.Opaque));
            _converter.FromUInt64(header.Cas, headerBytes.Slice(HeaderOffsets.Cas));

            _stream.Position = 0;
            Write(headerBytes);

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
                throw new InvalidOperationException("Operation spec is already in progress");
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
                throw new InvalidOperationException("An operation spec is not in progress.");
            }

            Span<byte> buffer = stackalloc byte[_operationSpecIsMutation ? 8 : 4];
            _converter.FromByte((byte) spec.OpCode, buffer);
            _converter.FromByte((byte) spec.PathFlags, buffer.Slice(1));
            _converter.FromUInt16((ushort) _operationSpecPathLength, buffer.Slice(2));

            if (_operationSpecIsMutation)
            {
                _converter.FromUInt32((uint) _operationSpecFragmentLength, buffer.Slice(4));
            }

            _stream.Position = _operationSpecStartPosition;
            Write(buffer);
            _stream.Position = _stream.Length;

            CurrentSegment = OperationSegment.Body;
        }

        /// <summary>
        /// After writing to the stream, records the number of bytes written to the current segment.
        /// </summary>
        /// <param name="bytes">Number of bytes written.</param>
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
                throw new InvalidOperationException("Cannot write data after the header has been written");
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
