using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal class ReplicaRead<T> : OperationBase<T>
    {
        private ReplicaRead(string key, ITypeTranscoder transcoder, IVBucket vBucket, uint opaque, uint timeout)
            : base(key, default(T), vBucket, transcoder, opaque, timeout)
        {
        }

        public ReplicaRead(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.ReplicaRead; }
        }

        public override byte[] CreateExtras()
        {
            return new byte[0];
        }

        public override byte[] Write()
        {
            var key = CreateKey();
            var header = CreateHeader(new byte[0], new byte[0], key);

            var buffer = new byte[key.GetLengthSafe() + header.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length, key.Length);

            return buffer;
        }

        public override IOperation Clone()
        {
            var cloned = new ReplicaRead<T>(Key, Transcoder, VBucket, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
        }

        public override bool RequiresKey
        {
            get { return true; }
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
