using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal sealed class Increment : MutationOperationBase<ulong>
    {
        internal Increment(string bucketName, string key) : base(bucketName, key)
        { }

        public ulong Delta { get; set; } = 1;

        public ulong? Initial { get; set; }

        public override OpCode OpCode => OpCode.Increment;

        protected override void WriteExtras(OperationBuilder builder)
        {
            const int extrasLength = sizeof(ulong) * 2 + sizeof(uint);

            var extras = builder.GetSpan(extrasLength);
            ByteConverter.FromUInt64(Delta, extras);

            if (Initial.HasValue)
            {
                ByteConverter.FromUInt64(Initial.Value, extras.Slice(sizeof(ulong)));
            }
            else
            {
                ByteConverter.FromUInt64(0, extras.Slice(sizeof(ulong)));

                Expires = 0xFFFFFFFF; //From KV RFC: When the Counter does not exist and no Initial value is provided, the Expiry needs to be all 1 bits for the operation to fail.
            }

            ByteConverter.FromUInt32(Expires, extras.Slice(sizeof(ulong) * 2));

            builder.Advance(extrasLength);
        }

        protected override void WriteBody(OperationBuilder builder)
        {
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
