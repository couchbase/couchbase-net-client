using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Increment : OperationBase<ulong>
    {
        private readonly ulong _delta;
        private readonly uint _expiration;
        private readonly ulong _initial;

        public Increment(string key, ulong initial, ulong delta, uint expiration, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, converter, transcoder, timeout)
        {
            _delta = delta;
            _initial = initial;
            _expiration = expiration;
        }

        private Increment(string key, ulong initial, ulong delta, uint expiration, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, initial, transcoder, vBucket, converter, opaque, timeout)
        {
            _delta = delta;
            _initial = initial;
            _expiration = expiration;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Increment; }
        }

        public override int BodyOffset
        {
            get { return 24; }
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[20];
            Converter.FromUInt64(_delta, extras, 0);
            Converter.FromUInt64(_initial, extras, 8);
            Converter.FromUInt32(_expiration, extras, 16);
            return extras;
        }

        public override byte[] CreateBody()
        {
            return new byte[0];
        }

        public override IOperation<ulong> Clone()
        {
            var cloned = new Increment(Key, _initial, _delta, _expiration, VBucket, Converter, Transcoder, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
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