using System;

namespace Couchbase.Core.IO.Operations.Collections
{
    internal class GetCid : OperationBase<string>
    {
        /// <summary>
        /// Creates a key either by the Key property or the Content property.
        /// <remarks>Early server versions used the Key for the collection name; later versions use the Content property.</remarks>
        /// </summary>
        public string CoerceKey => string.IsNullOrEmpty(Key) ? Content : Key;

        public override bool RequiresVBucketId => false;

        public override OpCode OpCode => OpCode.GetCidByName;

        public override string GetValue()
        {
            throw new NotImplementedException("Use GetValueAsUint() instead for GetCid result.");
        }

        public uint? GetValueAsUint()
        {
            if (Data.Length > 0)
            {
                try
                {
                    var buffer = Data;
                    ReadExtras(buffer.Span);
                    return Converters.ByteConverter.ToUInt32(buffer.Span.Slice(Header.ExtrasOffset + 8, 4));
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }

            return null;
        }
        protected override void WriteExtras(OperationBuilder builder)
        {
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
