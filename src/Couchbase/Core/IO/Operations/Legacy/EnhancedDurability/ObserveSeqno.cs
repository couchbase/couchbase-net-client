using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations.Legacy.EnhancedDurability
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

        /// <summary>
        /// Writes this instance into a memcached packet.
        /// </summary>
        /// <returns></returns>
        public override async Task SendAsync(IConnection connection)
        {
            var body = new byte[8];
            Converter.FromInt64(MutationToken.VBucketUuid, body, 0);

            var header = new byte[OperationHeader.Length];
            Converter.FromByte((byte)Magic.Request, header, HeaderOffsets.Magic);
            Converter.FromByte((byte)OpCode, header, HeaderOffsets.Opcode);
            Converter.FromInt16(MutationToken.VBucketId, header, HeaderOffsets.VBucket);
            Converter.FromInt32(body.Length, header, HeaderOffsets.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderOffsets.Opaque);

            var buffer = new byte[body.Length + header.Length];
            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length, body.Length);

            await connection.SendAsync(buffer, Completed).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the value of the memecached response packet and converts it to a <see cref="ObserveSeqnoResponse"/> instance.
        /// </summary>
        /// <returns></returns>
        public override ObserveSeqnoResponse GetValue()
        {
            var result = default(ObserveSeqnoResponse);
            if (Success && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span.Slice(Header.BodyOffset);

                    var isHardFailover = Converter.ToByte(buffer) == 1;
                    if (isHardFailover)
                    {
                        result = new ObserveSeqnoResponse
                        {
                            IsHardFailover = true,
                            VBucketId = Converter.ToInt16(buffer.Slice(1)),
                            VBucketUuid = Converter.ToInt64(buffer.Slice(3)),
                            LastPersistedSeqno = Converter.ToInt64(buffer.Slice(11)),
                            CurrentSeqno = Converter.ToInt64(buffer.Slice(19)),
                            OldVBucketUuid = Converter.ToInt64(buffer.Slice(27)),
                            LastSeqnoReceived = Converter.ToInt64(buffer.Slice(35))
                        };
                    }
                    else
                    {
                        result = new ObserveSeqnoResponse
                        {
                            IsHardFailover = false,
                            VBucketId = Converter.ToInt16(buffer.Slice(1)),
                            VBucketUuid = Converter.ToInt64(buffer.Slice(3)),
                            LastPersistedSeqno = Converter.ToInt64(buffer.Slice(11)),
                            CurrentSeqno = Converter.ToInt64(buffer.Slice(19)),
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
