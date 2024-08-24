using System;
using System.Runtime.ExceptionServices;

namespace Couchbase.Core.IO.Operations.Collections
{
    internal sealed class GetSid : OperationBase<uint?>
    {
        public override bool RequiresVBucketId => false;

        public override OpCode OpCode => OpCode.GetSidByName;

        public override uint? GetValue()
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
                    Exception = ExceptionDispatchInfo.Capture(e);
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }

            return 0u;
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
        }

        protected override void WriteBody(OperationBuilder builder)
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
