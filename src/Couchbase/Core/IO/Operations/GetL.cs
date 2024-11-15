using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal sealed class GetL<T> : Get<T>
    {
        /// <inheritdoc />
        public override bool IsReadOnly => false;

        protected override void WriteExtras(OperationBuilder builder)
        {
            var extras = builder.GetSpan(sizeof(uint));
            ByteConverter.FromUInt32(Expiry, extras);
            builder.Advance(sizeof(uint));
        }

        protected override void WriteBody(OperationBuilder builder)
        {
        }

        public uint Expiry { get; set; }

        public override OpCode OpCode => OpCode.GetL;
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
