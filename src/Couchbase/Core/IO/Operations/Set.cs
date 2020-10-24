namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Add a key to the database, replacing the key if it already exists.
    /// </summary>
    /// <typeparam name="T">The value to insert.</typeparam>
    internal sealed class Set<T> : MutationOperationBase<T>
    {
        internal Set(string bucketName, string key) : base(bucketName, key)
        {}

        public override OpCode OpCode => OpCode.Set;

        protected override bool SupportsRequestCompression => true;

        public override IOperation Clone()
        {
            var cloned = new Set<T>(BucketName, Key)
            {
                ReplicaIdx = ReplicaIdx,
                Content = Content,
                Transcoder = Transcoder,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                MutationToken = MutationToken,
                LastConfigRevisionTried = LastConfigRevisionTried,
                ErrorCode = ErrorCode,
                Expires = Expires
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

#endregion
