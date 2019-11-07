using System;
using Couchbase.KeyValue;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Represents an abstract base class for mutation operations (PROTOCOL_BINARY_CMD_SET, DELETE,REPLACE, ADD,
    /// APPEND, PREPEND, INCR, DECR, SET_WITH_META, DEL_WITH_META) and supports <see cref="OperationBase{}.MutationToken"/>'s.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class MutationOperationBase : OperationBase
    {
        public DurabilityLevel DurabilityLevel { get; set; }
        public TimeSpan? DurabilityTimeout { get; set; }

        /// <summary>
        /// Reads the VBucketUUID and Sequence Number from  the extras if the instance has a <see cref="OperationBase.VBucket"/> -
        /// only persistent Couchbase buckets that use VBucket Hashing support mutation tokens.
        /// </summary>
        /// <param name="buffer">The memcached response buffer.</param>
        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            TryReadMutationToken(buffer);
        }

        public override void WriteFramingExtras(OperationBuilder builder)
        {
            if (DurabilityLevel == DurabilityLevel.None)
            {
                return;
            }

            // TODO: omit timeout bytes if no timeout provided
            Span<byte> bytes = stackalloc byte[2];

            var framingExtra = new FramingExtraInfo(RequestFramingExtraType.DurabilityRequirements, (byte) (bytes.Length - 1));
            bytes[0] = framingExtra.Byte;
            bytes[1] = (byte) DurabilityLevel;

            // TODO: improve timeout, coerce to 1500ms, etc
            //var timeout = DurabilityTimeout.HasValue ? DurabilityTimeout.Value.TotalMilliseconds : 0;
            //Converter.FromUInt16((ushort)timeout, bytes, 2);

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
