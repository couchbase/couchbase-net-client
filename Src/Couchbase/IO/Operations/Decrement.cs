using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations
{
    internal sealed class Decrement : MutationOperationBase<ulong>
    {
        public Decrement(string key, ulong initial, ulong delta, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            Delta = delta;
            Initial = initial;
        }

        private Decrement(string key, ulong initial, ulong delta, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, initial, vBucket, transcoder, opaque, timeout)
        {
            Delta = delta;
            Initial = initial;
        }
        public ulong Delta { get; }

        public ulong Initial { get; }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Decrement; }
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[20];
            Converter.FromUInt64(Delta, extras, 0);
            Converter.FromUInt64(Initial, extras, 8);
            Converter.FromUInt32(Expires, extras, 16);
            return extras;
        }

        public override byte[] CreateBody()
        {
            return new byte[0];
        }

        public override IOperation Clone()
        {
            var cloned = new Decrement(Key, Initial, Delta, VBucket, Transcoder, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                MutationToken = MutationToken,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode,
                Expires = Expires
            };
            return cloned;
        }

        public override bool CanRetry()
        {
            return false;
        }
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
