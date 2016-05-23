
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Represents an abstract base class for mutation operations (PROTOCOL_BINARY_CMD_SET, DELETE,REPLACE, ADD,
    /// APPEND, PREPEND, INCR, DECR, SET_WITH_META, DEL_WITH_META) and supports <see cref="OperationBase{}.MutationToken"/>'s.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class MutationOperationBase<T> : OperationBase<T>
    {
        protected MutationOperationBase(string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, value, vBucket, transcoder, opaque, timeout)
        {
        }

        protected MutationOperationBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        /// <summary>
        /// Reads the VBucketUUID and Sequence Number from  the extras if the instance has a <see cref="VBucket"/> -
        /// only persistent Couchbase buckets that use VBucket Hashing support mutation tokens.
        /// </summary>
        /// <param name="buffer">The memcached response buffer.</param>
        public override void ReadExtras(byte[] buffer)
        {
            if (buffer.Length >= 40 && VBucket != null)
            {
                var uuid = Converter.ToInt64(buffer, 24);
                var seqno = Converter.ToInt64(buffer, 32);
                MutationToken = new MutationToken(VBucket.BucketName, (short)VBucket.Index, uuid, seqno);
            }
        }

        public override int BodyOffset
        {
            get { return HeaderLength + Header.ExtrasLength; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
