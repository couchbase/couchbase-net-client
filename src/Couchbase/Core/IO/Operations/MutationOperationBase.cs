using System;
using Couchbase.KeyValue;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{

    /// <summary>
    /// Represents an abstract base class for mutation operations (PROTOCOL_BINARY_CMD_SET, DELETE,REPLACE, ADD,
    /// APPEND, PREPEND, INCR, DECR, SET_WITH_META, DEL_WITH_META) and supports <see cref="OperationBase.MutationToken"/>'s.
    /// </summary>
    internal abstract class MutationOperationBase : OperationBase
    {
        ushort SyncReplicationTimeoutFloorMs = 1500;

        public DurabilityLevel DurabilityLevel { get; set; }
        public TimeSpan? DurabilityTimeout { get; set; }

        /// <inheritdoc />
        public override bool IsReadOnly => false;

        /// <summary>
        /// Reads the VBucketUUID and Sequence Number from the extras if the instance has a <see cref="OperationBase.VBucketId"/> -
        /// only persistent Couchbase buckets that use VBucket Hashing support mutation tokens.
        /// </summary>
        /// <param name="buffer">The memcached response buffer.</param>
        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            TryReadMutationToken(buffer);

            TryReadServerDuration(buffer);
        }

        protected override void WriteFramingExtras(OperationBuilder builder)
        {
            if (DurabilityLevel == DurabilityLevel.None)
            {
                return;
            }

            // TODO: omit timeout bytes if no timeout provided
            Span<byte> bytes = stackalloc byte[4];

            var framingExtra = new FramingExtraInfo(RequestFramingExtraType.DurabilityRequirements, (byte) (bytes.Length - 1));
            bytes[0] = (byte) (framingExtra.Byte | (byte) 0x03);
            bytes[1] = (byte) DurabilityLevel;

            var userTimeout = DurabilityTimeout.Value.TotalMilliseconds;

            ushort deadline;
            if (userTimeout >= ushort.MaxValue) {
                // -1 because 0xffff is going to be reserved by the cluster. 1ms less doesn't matter.
                deadline = ushort.MaxValue - 1;
            } else {
                // per spec 90% of the timeout is used as the deadline
                deadline = (ushort) (userTimeout * 0.9);
            }

            if (deadline < SyncReplicationTimeoutFloorMs) {
                // we need to ensure a floor value
                deadline = SyncReplicationTimeoutFloorMs;
            }

            ByteConverter.FromUInt16(deadline, bytes);
            builder.Write(bytes);
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
