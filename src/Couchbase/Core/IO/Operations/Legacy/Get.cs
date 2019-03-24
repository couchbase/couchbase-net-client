using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal class Get<T> : OperationBase<T>
    {
        public override OpCode OpCode => OpCode.Get;

        public override byte[] Write()
        {
            var key = CreateKey();
            var framingExtras = CreateFramingExtras();
            var header = CreateHeader(Array.Empty<byte>(), Array.Empty<byte>(), key, framingExtras);

            var buffer = new byte[key.GetLengthSafe() + header.GetLengthSafe() + framingExtras.GetLengthSafe()];
            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(framingExtras, 0, buffer, header.Length, framingExtras.Length);
            Buffer.BlockCopy(key, 0, buffer, header.Length + framingExtras.Length, key.Length);

            return buffer;
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > Header.ExtrasOffset)
            {
                var format = new byte();
                var flags = Converter.ToByte(buffer.Slice(Header.ExtrasOffset));
                Converter.SetBit(ref format, 0, Converter.GetBit(flags, 0));
                Converter.SetBit(ref format, 1, Converter.GetBit(flags, 1));
                Converter.SetBit(ref format, 2, Converter.GetBit(flags, 2));
                Converter.SetBit(ref format, 3, Converter.GetBit(flags, 3));

                var compression = new byte();
                Converter.SetBit(ref compression, 4, Converter.GetBit(flags, 4));
                Converter.SetBit(ref compression, 5, Converter.GetBit(flags, 5));
                Converter.SetBit(ref compression, 6, Converter.GetBit(flags, 6));

                var typeCode = (TypeCode)(Converter.ToUInt16(buffer.Slice(26)) & 0xff);
                Format = (DataFormat)format;
                Compression = (Compression)compression;
                Flags.DataFormat = Format;
                Flags.Compression = Compression;
                Flags.TypeCode = typeCode;
            }
        }

        public override IOperation Clone()
        {
            var cloned = new Get<T>
            {
                Key = Key,
                Content = Content,
                Transcoder = Transcoder,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
        }

        public override bool CanRetry()
        {
            return ErrorCode == null || ErrorMapRequestsRetry();
        }

        public override bool RequiresKey => true;
    }
}

#region [ License information ]

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

#endregion [ License information ]
