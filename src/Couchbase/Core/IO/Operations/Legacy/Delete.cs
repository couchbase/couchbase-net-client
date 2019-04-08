using System;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal sealed class Delete : MutationOperationBase
    {
        public override OpCode OpCode => OpCode.Delete;

        public override void WriteExtras(OperationBuilder builder)
        {
        }

        public override void WriteFramingExtras(OperationBuilder builder)
        {
        }

        public override IOperation Clone()
        {
            var cloned = new Delete
            {
                Key = Key,
                Transcoder = Transcoder,
                VBucketId = VBucketId,
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                Opaque = Opaque,
                MutationToken = MutationToken,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
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
