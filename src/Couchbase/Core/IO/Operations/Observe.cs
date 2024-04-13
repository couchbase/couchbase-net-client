using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.Utils;

namespace Couchbase.Core.IO.Operations
{
    internal sealed class Observe : OperationBase<ObserveState>
    {
        protected override void WriteExtras(OperationBuilder builder)
        {
        }

        protected override void WriteFramingExtras(OperationBuilder builder)
        {
        }

        protected override void WriteKey(OperationBuilder builder)
        {
        }

        [SkipLocalsInit]
        protected override void WriteBody(OperationBuilder builder)
        {
            Span<byte> buffer = stackalloc byte[OperationHeader.MaxKeyLength + Leb128.MaxLength + 4];

            var keyLength = WriteKey(buffer.Slice(4));

            // ReSharper disable once PossibleInvalidOperationException
            ByteConverter.FromInt16(VBucketId.Value, buffer);
            ByteConverter.FromInt16((short) keyLength, buffer.Slice(2));

            builder.Write(buffer.Slice(0, keyLength + 4));
        }

        public override ObserveState GetValue()
        {
            if (Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span;
                    var keylength = ByteConverter.ToInt16(buffer.Slice(26));

                    return new ObserveState
                    {
                        PersistStat = ByteConverter.ToUInt32(buffer.Slice(16)),
                        ReplState = ByteConverter.ToUInt32(buffer.Slice(20)),
                        VBucket = ByteConverter.ToInt16(buffer.Slice(24)),
                        KeyLength = keylength,
                        Key = ByteConverter.ToString(buffer.Slice(28, keylength)),
                        KeyState = (KeyState) buffer[28 + keylength],
                        Cas = ByteConverter.ToUInt64(buffer.Slice(28 + keylength + 1))
                    };
                }
                catch (Exception e)
                {
                    Exception = ExceptionDispatchInfo.Capture(e);
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return new ObserveState();
        }
        public override OpCode OpCode => OpCode.Observe;
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
