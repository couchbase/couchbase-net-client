using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations.EnhancedDurability
{
    internal class ObserveSeqno : OperationBase<ObserveSeqnoResponse>
    {
        /// <summary>
        /// Gets the operation code for <see cref="OpCode"/>
        /// </summary>
        /// <value>
        /// The operation code.
        /// </value>
        public override OpCode OpCode => OpCode.ObserveSeqNo;

        public override void WriteExtras(OperationBuilder builder)
        {
        }

        public override void WriteFramingExtras(OperationBuilder builder)
        {
        }

        public override void WriteKey(OperationBuilder builder)
        {
        }

        /// <summary>
        /// Gets the value of the memecached response packet and converts it to a <see cref="ObserveSeqnoResponse"/> instance.
        /// </summary>
        /// <returns></returns>
        public override ObserveSeqnoResponse GetValue()
        {
            var result = default(ObserveSeqnoResponse);
            if (Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span.Slice(Header.BodyOffset);

                    var isHardFailover = buffer[0] == 1;
                    if (isHardFailover)
                    {
                        result = new ObserveSeqnoResponse
                        {
                            IsHardFailover = true,
                            VBucketId = ByteConverter.ToInt16(buffer.Slice(1)),
                            VBucketUuid = ByteConverter.ToInt64(buffer.Slice(3)),
                            LastPersistedSeqno = ByteConverter.ToInt64(buffer.Slice(11)),
                            CurrentSeqno = ByteConverter.ToInt64(buffer.Slice(19)),
                            OldVBucketUuid = ByteConverter.ToInt64(buffer.Slice(27)),
                            LastSeqnoReceived = ByteConverter.ToInt64(buffer.Slice(35))
                        };
                    }
                    else
                    {
                        result = new ObserveSeqnoResponse
                        {
                            IsHardFailover = false,
                            VBucketId = ByteConverter.ToInt16(buffer.Slice(1)),
                            VBucketUuid = ByteConverter.ToInt64(buffer.Slice(3)),
                            LastPersistedSeqno = ByteConverter.ToInt64(buffer.Slice(11)),
                            CurrentSeqno = ByteConverter.ToInt64(buffer.Slice(19)),
                        };
                    }
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            var cloned = new ObserveSeqno
            {
                MutationToken = MutationToken,
                Key = Key,
                ReplicaIdx = ReplicaIdx,
                Content = Content,
                Transcoder = Transcoder,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Attempts = Attempts,
                CreationTime = CreationTime,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
        }

        public override bool RequiresKey => false;
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
