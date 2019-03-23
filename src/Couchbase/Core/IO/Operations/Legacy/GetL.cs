using System;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal class GetL<T> : Get<T>
    {
        public override byte[] CreateExtras()
        {
            var extras = new byte[4];
            Converter.FromUInt32(Expiration, extras, 0);
            return extras;
        }

        public override byte[] Write()
        {
            var key = CreateKey();
            var extras = CreateExtras();
            var framingExtras = CreateFramingExtras();
            var header = CreateHeader(extras, Array.Empty<byte>(), key, framingExtras);

            var buffer = new byte[header.GetLengthSafe() + key.GetLengthSafe() + extras.GetLengthSafe() + framingExtras.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(framingExtras, 0, buffer, header.Length, framingExtras.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length + framingExtras.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + framingExtras.Length + extras.Length, key.Length);

            return buffer;
        }

        public uint Expiration { get; set; }

        public override OpCode OpCode => OpCode.GetL;

        public override IOperation Clone()
        {
            var cloned = new GetL<T>
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
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
