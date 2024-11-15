using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal sealed class Touch : MutationOperationBase
    {
        internal static void WriteExpiry(OperationBuilder builder, uint expires)
        {
            var extras = builder.GetSpan(sizeof(uint));
            ByteConverter.FromUInt32(expires, extras);
            builder.Advance(sizeof(uint));
        }

        internal static bool TryReadNewExpiry(ReadOnlySpan<byte> buffer, int extrasLength, int extrasOffset, out uint expiry)
        {
            if (extrasLength >= sizeof(uint) && buffer.Length >= extrasOffset + extrasLength)
            {
                var expiryInExtras = buffer.Slice(extrasOffset);
                expiry = ByteConverter.ToUInt32(expiryInExtras);
                return true;
            }

            expiry = 0;
            return false;
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
            WriteExpiry(builder, Expires);
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

        public override OpCode OpCode => OpCode.Touch;
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
