using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Noop : OperationBase
    {
        public Noop(ITypeTranscoder transcoder, uint timeout)
            : this(string.Empty, null, transcoder, timeout)
        {
        }
        public Noop(string key, ITypeTranscoder transcoder, IVBucket vBucket, uint opaque, uint timeout)
            : base(key, vBucket, transcoder, opaque, timeout)
        {
        }

        public Noop(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.NoOp; }
        }

        public override byte[] Write()
        {
            return CreateHeader(new byte[0], new byte[0], null);
        }

        public override bool RequiresKey
        {
            get { return false; }
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
