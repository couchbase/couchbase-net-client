using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal class GetT<T> : MutationOperationBase<T>
    {
        internal GetT(string bucketName, string key) : base(bucketName, key)
        { }

        protected override void WriteExtras(OperationBuilder builder)
        {
            Touch.WriteExpiry(builder, Expires);
        }

        protected override void WriteBody(OperationBuilder builder)
        {
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            // do not call MutationOperationBase.ReadExtras, as Touch operations do not contain MutationToken
            if (Touch.TryReadNewExpiry(buffer, Header.ExtrasLength, Header.ExtrasOffset, out var newExpiry))
            {
                Expires = newExpiry;
            }
        }

        public override OpCode OpCode => OpCode.GAT;
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
