namespace Couchbase.Core.IO.Operations
{
    internal sealed class Prepend<T> : MutationOperationBase<T>
    {
        internal Prepend(string bucketName, string key) : base(bucketName, key)
        { }

        protected override void BeginSend()
        {
            Flags = Transcoder.GetFormat(Content);
            Format = Flags.DataFormat;
            Compression = Flags.Compression;
        }

        public override void WriteExtras(OperationBuilder builder)
        {
        }

        public override OpCode OpCode => OpCode.Prepend;

        public override IOperation Clone()
        {
            var cloned = new Prepend<T>(BucketName, Key)
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
                ErrorCode = ErrorCode
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
