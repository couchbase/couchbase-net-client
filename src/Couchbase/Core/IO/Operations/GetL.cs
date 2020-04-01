using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal class GetL<T> : Get<T>
    {
        public override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> extras = stackalloc byte[4];
            ByteConverter.FromUInt32(Expiry, extras);
            builder.Write(extras);
        }

        public override void WriteBody(OperationBuilder builder)
        {
        }

        public uint Expiry { get; set; }

        public override OpCode OpCode => OpCode.GetL;

        public override bool Idempotent { get; } = false;

        public override IOperation Clone()
        {
            var cloned = new GetL<T>
            {
                Key = Key,
                ReplicaIdx = ReplicaIdx,
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
