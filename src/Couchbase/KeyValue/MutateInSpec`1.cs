#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Strongly-typed variant of <see cref="MutateInSpec"/>.
    /// </summary>
    /// <typeparam name="T">Type of <see cref="OperationSpec.Value"/>.</typeparam>
    internal sealed class MutateInSpec<T> : MutateInSpec
    {
        public MutateInSpec(OpCode opCode, string path, T value) : base(opCode, path)
        {
            Value = value;
        }

        private bool TryGetTypedValue([NotNullWhen(true)] out T? value)
        {
            if (Value is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        // Override so calls to transcoder and serializer use the generic type T instead of object
        /// <inheritdoc />
        internal override void WriteSpecValue(OperationBuilder builder, ITypeTranscoder transcoder)
        {
            if (!TryGetTypedValue(out var typedValue))
            {
                // Fallback to legacy behavior if the Value is changed after creation to a value not of type T
                base.WriteSpecValue(builder, transcoder);
                return;
            }

            if (!RemoveBrackets)
            {
                // We can serialize directly
                if (this is { OpCode: OpCode.Set, Path.Length: 0 })
                {
                    // We're writing the entire document, this should pass through the transcoder not just the serializer.
                    // This allows the use of XATTRs with non-JSON documents.
                    var flags = transcoder.GetFormat(typedValue);
                    transcoder.Encode<T>(builder, typedValue, flags, OpCode.Set);
                }
                else
                {
                    transcoder.Serializer!.Serialize(builder, typedValue);
                }
            }
            else
            {
                using var stream = MemoryStreamFactory.GetMemoryStream();
                transcoder.Serializer!.Serialize(stream, typedValue);

                ReadOnlyMemory<byte> bytes = stream.GetBuffer().AsMemory(0, (int) stream.Length);
                bytes = bytes.StripBrackets();

                builder.Write(bytes);
            }
        }

        /// <inheritdoc />
        internal override OperationSpec Clone()
        {
            if (!TryGetTypedValue(out var typedValue))
            {
                // Fallback to legacy behavior if the Value is changed after creation to a value not of type T
                return base.Clone();
            }

            return new MutateInSpec<T>(OpCode, Path, typedValue)
            {
                Bytes = ReadOnlyMemory<byte>.Empty,
                PathFlags = PathFlags,
                DocFlags = DocFlags,
                RemoveBrackets = RemoveBrackets,
                Status = ResponseStatus.None
            };
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2023 Couchbase, Inc.
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
