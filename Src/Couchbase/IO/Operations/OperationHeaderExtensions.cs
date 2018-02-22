using System;
using System.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal static class OperationHeaderExtensions
    {
        private static readonly IByteConverter Converter = new DefaultConverter();

        internal static OperationHeader CreateHeader(this SocketAsyncState state, out ErrorCode errorCode)
        {
            if (state.Data == null || state.Data.Length < HeaderIndexFor.HeaderLength)
            {
                errorCode = null;
                return new OperationHeader { Status = ResponseStatus.None };
            }

            // take first 24 bytes of the buffer to create the header then reset stream position
            var buffer = new byte[24];
            state.Data.Position = 0;
            state.Data.Read(buffer, 0, buffer.Length);
            state.Data.Position = 0;

            return CreateHeader(buffer, state.ErrorMap, out errorCode);
        }

        internal static OperationHeader CreateHeader(this byte[] buffer)
        {
            return CreateHeader(buffer, null, out var _);
        }

        internal static OperationHeader CreateHeader(this byte[] buffer, ErrorMap errorMap, out ErrorCode errorCode)
        {
            if (buffer == null || buffer.Length < HeaderIndexFor.HeaderLength)
            {
                errorCode = null;
                return new OperationHeader {Status = ResponseStatus.None};
            }

            int keyLength, framingExtrasLength;
            var magic = (Magic) Converter.ToByte(buffer, HeaderIndexFor.Magic);
            if (magic == Magic.AltResponse)
            {
                framingExtrasLength = Converter.ToByte(buffer, HeaderIndexFor.FramingExtras);
                keyLength = Converter.ToByte(buffer, HeaderIndexFor.AltKeyLength);
            }
            else
            {
                framingExtrasLength = 0;
                keyLength = Converter.ToInt16(buffer, HeaderIndexFor.KeyLength);
            }

            var statusCode = Converter.ToInt16(buffer, HeaderIndexFor.Status);
            var status = GetResponseStatus(statusCode, errorMap, out errorCode);

            return new OperationHeader
            {
                Magic = (byte) magic,
                OperationCode = Converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                FramingExtrasLength = framingExtrasLength,
                KeyLength = keyLength,
                ExtrasLength = Converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                DataType = (DataType) Converter.ToByte(buffer, HeaderIndexFor.Datatype),
                Status = status,
                BodyLength = Converter.ToInt32(buffer, HeaderIndexFor.Body),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                Cas = Converter.ToUInt64(buffer, HeaderIndexFor.Cas)
            };
        }

        internal static ResponseStatus GetResponseStatus(short code, ErrorMap errorMap, out ErrorCode errorCode)
        {
            var status = (ResponseStatus) code;

            // Is it a known response status?
            if (Enum.IsDefined(typeof(ResponseStatus), status))
            {
                errorCode = null;
                return status;
            }

            // If available, try and use the error map to get more details
            if (errorMap != null && errorMap.TryGetGetErrorCode(code, out errorCode))
            {
                return ResponseStatus.Failure;
            }

            errorCode = null;
            return ResponseStatus.UnknownError;
        }

        internal static long? GetServerDuration(this OperationHeader header, MemoryStream stream)
        {
            if (header.FramingExtrasLength <= 0)
            {
                return null;
            }

            // copy framing extra bytes then reset steam position
            var bytes = new byte[header.FramingExtrasLength];
            stream.Position = HeaderIndexFor.HeaderLength;
            stream.Read(bytes, 0, header.FramingExtrasLength);
            stream.Position = 0;

            return GetServerDuration(bytes);
        }

        internal static long? GetServerDuration(this OperationHeader header, byte[] buffer)
        {
            if (header.FramingExtrasLength <= 0)
            {
                return null;
            }

            // copy framing extra bytes
            var bytes = new byte[header.FramingExtrasLength];
            Buffer.BlockCopy(buffer, HeaderIndexFor.HeaderLength, bytes, 0, header.FramingExtrasLength);

            return GetServerDuration(bytes);
        }

        internal static long? GetServerDuration(byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var control = buffer[offset++];
                var type = (FramingExtraType) (control & 0xF0); // first 4 bits
                var length = control & 0x0F; // last 4 bits

                if (type == FramingExtraType.ServerDuration)
                {
                    // read encoded two byte server duration
                    var encoded = Converter.ToUInt16(buffer, offset);
                    if (encoded > 0)
                    {
                        // decode into microseconds
                        return (long) Math.Pow(encoded, 1.74) / 2;
                    }
                }

                offset += length;
            }

            return null;
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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

#endregion
