using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal class Increment : MutationOperationBase<ulong>
    {
        internal Increment(string bucketName, string key) : base(bucketName, key)
        { }

        public ulong Delta { get; set; } = 1;

        public ulong? Initial { get; set; }

        public override OpCode OpCode => OpCode.Increment;

        protected override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> extras = stackalloc byte[20];
            ByteConverter.FromUInt64(Delta, extras);
            if (Initial.HasValue) ByteConverter.FromUInt64(Initial.Value, extras.Slice(8));
            ByteConverter.FromUInt32(Expires, extras.Slice(16));
            builder.Write(extras);
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
