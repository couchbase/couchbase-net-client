
using System;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.EnhancedDurability
{
    internal class ObserveSeqno : OperationBase<ObserveSeqnoResponse>
    {
        public ObserveSeqno(MutationToken mutationToken, ITypeTranscoder transcoder, uint timeout)
            : base(null, null, transcoder, timeout)
        {
            MutationToken = mutationToken;
        }

        /// <summary>
        /// Gets the operation code for <see cref="OperationCode"/>
        /// </summary>
        /// <value>
        /// The operation code.
        /// </value>
        public override OperationCode OperationCode
        {
            get { return OperationCode.ObserveSeqNo; }
        }

        /// <summary>
        /// Writes this instance into a memcached packet.
        /// </summary>
        /// <returns></returns>
        public override byte[] Write()
        {
            var body = new byte[8];
            Converter.FromInt64(MutationToken.VBucketUUID, body, 0);

            var header = new byte[24];
            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);
            Converter.FromInt16(MutationToken.VBucketId, header, HeaderIndexFor.VBucket);
            Converter.FromInt32(body.Length, header, HeaderIndexFor.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderIndexFor.Opaque);

            var buffer = new byte[body.Length + header.Length];
            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length, body.Length);
            return buffer;
        }

        /// <summary>
        /// Gets the value of the memecached response packet and converts it to a <see cref="ObserveSeqnoResponse"/> instance.
        /// </summary>
        /// <returns></returns>
        public override ObserveSeqnoResponse GetValue()
        {
            var result = default(ObserveSeqnoResponse);
            if (Success && Data != null && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.ToArray();
                    var offset = Header.BodyOffset;

                    var isHardFailover = Converter.ToByte(buffer, offset) == 1;
                    if (isHardFailover)
                    {
                        result = new ObserveSeqnoResponse
                        {
                            IsHardFailover = isHardFailover,
                            VBucketId = Converter.ToInt16(buffer, offset + 1),
                            VBucketUUID = Converter.ToInt64(buffer, offset + 3),
                            LastPersistedSeqno = Converter.ToInt64(buffer, offset + 11),
                            CurrentSeqno = Converter.ToInt64(buffer, offset + 19),
                            OldVBucketUUID = Converter.ToInt64(buffer, offset + 27),
                            LastSeqnoReceived = Converter.ToInt64(buffer, offset + 35)
                        };
                    }
                    else
                    {
                        result = new ObserveSeqnoResponse
                        {
                            IsHardFailover = isHardFailover,
                            VBucketId = Converter.ToInt16(buffer, offset + 1),
                            VBucketUUID = Converter.ToInt64(buffer, offset + 3),
                            LastPersistedSeqno = Converter.ToInt64(buffer, offset + 11),
                            CurrentSeqno = Converter.ToInt64(buffer, offset + 19),
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
            var cloned = new ObserveSeqno(MutationToken, Transcoder, Timeout)
            {
                Attempts = Attempts,
                CreationTime = CreationTime,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
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
