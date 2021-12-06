using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Couchbase.Core.IO.Converters;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    internal abstract class OperationBase<T> : OperationBase, IOperation<T>
    {
        [MaybeNull]
        public T Content { get; set; }

        protected override void WriteBody(OperationBuilder builder)
        {
            if (typeof(T).GetTypeInfo().IsValueType || Content != null)
            {
                Transcoder.Encode(builder, Content!, Flags, OpCode);
            }
        }

        /// <inheritdoc />
        [return: MaybeNull]
        public virtual T GetValue()
        {
            var result = default(T);
            if(Data.Length > 0)
            {
                try
                {
                    var buffer = Data;
                    ReadExtras(buffer.Span);
                    var offset = Header.BodyOffset;
                    var length = Header.TotalLength - Header.BodyOffset;

                    if ((Header.DataType & DataType.Snappy) != DataType.None)
                    {
                        using var decompressed = OperationCompressor.Decompress(buffer.Slice(offset, length), Span);
                        result = Transcoder.Decode<T>(decompressed.Memory, Flags, OpCode);
                    }
                    else
                    {
                        result = Transcoder.Decode<T>(buffer.Slice(offset, length), Flags, OpCode);
                    }
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> extras = stackalloc byte[8];

            Flags = Transcoder.GetFormat(Content!);
            Flags.Write(extras);

            ByteConverter.FromUInt32(Expires, extras.Slice(4));

            builder.Write(extras);
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
