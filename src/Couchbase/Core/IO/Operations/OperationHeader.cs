using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Couchbase.Utils;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#else
using System.Collections.Generic;
#endif

namespace Couchbase.Core.IO.Operations
{
    [StructLayout(LayoutKind.Explicit, Size = Length)]
    internal struct OperationHeader
    {
        public const int Length = 24;
        public const int MaxKeyLength = 250;

#if NET8_0_OR_GREATER
        private static readonly FrozenSet<ResponseStatus> ValidResponseStatuses =
            Enum.GetValues<ResponseStatus>().ToFrozenSet();
#elif NET6_0_OR_GREATER
        private static readonly HashSet<ResponseStatus> ValidResponseStatuses =
            new(Enum.GetValues<ResponseStatus>());
#else
        private static readonly HashSet<ResponseStatus> ValidResponseStatuses =
            new((ResponseStatus[]) Enum.GetValues(typeof(ResponseStatus)));
#endif

        [FieldOffset(HeaderOffsets.Magic)]
        private Magic _magic;
        public Magic Magic
        {
            readonly get => _magic;
            set => _magic = value;
        }

        [FieldOffset(HeaderOffsets.Opcode)]
        private OpCode _opCode;
        public OpCode OpCode
        {
            readonly get => _opCode;
            set => _opCode = value;
        }

        // If Magic is Magic.AltResponse then the first (high) byte is the framing extras and the second (low) byte is the key length.
        // Otherwise the full value is the key length.

        [FieldOffset(HeaderOffsets.KeyLength)]
        private short _keyAndFramingExtrasLength;
        public byte FramingExtrasLength
        {
            readonly get => IsAlternateFormat ? unchecked((byte)(_keyAndFramingExtrasLength >>> 8)) : (byte)0;
            set
            {
                if (!IsAlternateFormat)
                {
                    if (value != 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException("FramingExtrasLength is only valid for Magic.AltResponse");
                    }

                    return;
                }

                _keyAndFramingExtrasLength = (short)((value << 8) | (_keyAndFramingExtrasLength & 0xff));
            }
        }

        public int KeyLength
        {
            readonly get => IsAlternateFormat ? _keyAndFramingExtrasLength & 0xff : _keyAndFramingExtrasLength;
            set
            {
                unchecked
                {
                    // Negative value will be a very large uint, there is minor perf gain checking range this way because
                    // it requires only a single comparison instead of two.
                    if ((uint)value > MaxKeyLength)
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value));
                    }

                    if (IsAlternateFormat)
                    {
                        // This should already be validated by the precondition above
                        Debug.Assert(value <= byte.MaxValue);

                        _keyAndFramingExtrasLength = (short)((_keyAndFramingExtrasLength & 0xff00) | value);
                    }
                    else
                    {
                        _keyAndFramingExtrasLength = (short)value;
                    }
                }
            }
        }

        [FieldOffset(HeaderOffsets.ExtrasLength)]
        private byte _extrasLength;
        public byte ExtrasLength
        {
            readonly get => _extrasLength;
            set => _extrasLength = value;
        }

        [FieldOffset(HeaderOffsets.Datatype)]
        private DataType _dataType;
        public DataType DataType
        {
            readonly get => _dataType;
            set => _dataType = value;
        }

        [FieldOffset(HeaderOffsets.Status)]
        private short _status;
        public ResponseStatus Status
        {
            readonly get => (ResponseStatus)_status;
            set => _status = (short)value;
        }

        [FieldOffset(HeaderOffsets.BodyLength)]
        private int _bodyLength;
        public int BodyLength
        {
            readonly get => _bodyLength;
            set
            {
                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value));
                }

                _bodyLength = value;
            }
        }

        [FieldOffset(HeaderOffsets.Opaque)]
        private uint _opaque;
        public uint Opaque
        {
            readonly get => _opaque;
            set => _opaque = value;
        }

        [FieldOffset(HeaderOffsets.Cas)]
        private ulong _cas;
        public ulong Cas
        {
            readonly get => _cas;
            set => _cas = value;
        }

        public readonly bool IsAlternateFormat
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // All non-Alt formats have the high bit set
                return ((byte)Magic & 0x80) == 0;
            }
        }

        public readonly int TotalLength => BodyLength + Length;
        public readonly int ExtrasOffset => Length + FramingExtrasLength;
        public readonly int BodyOffset
        {
            // We could calculate purely using properties, but this approach reduces branching by only checking
            // the value of Magic once.
            get
            {
                var offset = Length + ExtrasLength;

                if (IsAlternateFormat)
                {
                    offset += (_keyAndFramingExtrasLength & 0xff) + ((_keyAndFramingExtrasLength >>> 8) & 0xff);
                }
                else
                {
                    offset += _keyAndFramingExtrasLength;
                }

                return offset;
            }
        }

        public static OperationHeader Read(ReadOnlySpan<byte> buffer)
        {
            if (!TryRead(buffer, out var header))
            {
                // header is already filled with 0s by TryRead, just set Status to None
                header.Status = ResponseStatus.None;
            }

            return header;
        }

        public static bool TryRead(ReadOnlySpan<byte> buffer, out OperationHeader header)
        {
            if (buffer.Length < Unsafe.SizeOf<OperationHeader>())
            {
                header = default;
                return false;
            }

#if NET6_0_OR_GREATER
            // Ensure future edits don't add reference types to this struct. This will cause local
            // debugging and unit tests to throw an exception on modern .NET versions.
            Debug.Assert(!RuntimeHelpers.IsReferenceOrContainsReferences<OperationHeader>());
#endif

            // We've validated sufficient length and this type includes only value types so this
            // unsafe operation is really safe
            header = Unsafe.ReadUnaligned<OperationHeader>(ref MemoryMarshal.GetReference(buffer));

            if (BitConverter.IsLittleEndian)
            {
                // Incoming multi-byte values are all big endian, so convert them to little endian,
                // except for Opaque which is little endian instead. This is because most CPU architectures are
                // little endian and the value is opaque to the server, so we can keep the more prevalent byte order.

                header._keyAndFramingExtrasLength = BinaryPrimitives.ReverseEndianness(header._keyAndFramingExtrasLength);
                header._status = BinaryPrimitives.ReverseEndianness(header._status);
                header._bodyLength = BinaryPrimitives.ReverseEndianness(header._bodyLength);
                header._cas = BinaryPrimitives.ReverseEndianness(header._cas);
            }
            else
            {
                // TODO: Change all reads/writes of Opaque to use CPU byte ordering to avoid reversals on all architectures
                header._opaque = BinaryPrimitives.ReverseEndianness(header._opaque);
            }

            // Is it a known response status?
            if (!ValidResponseStatuses.Contains(header.Status))
            {
                header.Status = ResponseStatus.UnknownError;
            }

            return true;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion [ License information          ]
