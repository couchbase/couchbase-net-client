using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Core.IO.Converters;
using Couchbase.KeyValue;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations.SubDocument
{
    internal class MultiLookup<T> : OperationBase<T>, IEquatable<MultiLookup<T>>
    {
        public ReadOnlyCollection<LookupInSpec> LookupCommands { get; }
        public SubdocDocFlags DocFlags { get; set; }

        public MultiLookup(string key, IEnumerable<LookupInSpec> specs)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (specs == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(specs));
            }

            Key = key;

            var commands = specs.ToList();

            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].OriginalIndex = i;
            }

            // re-order the specs so XAttrs come first.
            commands.Sort(OperationSpec.ByXattr);

            LookupCommands = new ReadOnlyCollection<LookupInSpec>(commands);
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
            if (DocFlags != SubdocDocFlags.None)
            {
                //Add the doc flags
                Span<byte> buffer = stackalloc byte[sizeof(byte)];
                buffer[0] = (byte)DocFlags;
                builder.Write(buffer);
            }
        }

        protected override void WriteFramingExtras(OperationBuilder builder)
        {
        }

        protected override void WriteBody(OperationBuilder builder)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(OperationSpec.MaxPathLength);
            try
            {
                var bufferSpan = buffer.AsSpan();

                foreach (var lookup in LookupCommands)
                {
                    var pathLength = ByteConverter.FromString(lookup.Path, bufferSpan);
                    builder.BeginOperationSpec(false);
                    builder.Write(buffer, 0, pathLength);
                    builder.CompleteOperationSpec(lookup);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override OpCode OpCode => OpCode.MultiLookup;

        public IList<LookupInSpec> GetCommandValues()
        {
            if (Data.IsEmpty)
            {
                return LookupCommands;
            }

            var responseSpan = Data.Span.Slice(Header.BodyOffset);
            var commandIndex = 0;

            for (; ;)
            {
                var bodyLength = ByteConverter.ToInt32(responseSpan.Slice(2));
                var payLoad = new byte[bodyLength];
                responseSpan.Slice(6, bodyLength).CopyTo(payLoad);

                var command = LookupCommands[commandIndex++];
                command.Status = (ResponseStatus)ByteConverter.ToUInt16(responseSpan);
                command.ValueIsJson = payLoad.AsSpan().IsJson();
                command.Bytes = payLoad;

                responseSpan = responseSpan.Slice(6 + bodyLength);
                if (responseSpan.Length <= 0) break;
            }

            return LookupCommands;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(MultiLookup<T>? other)
        {
            if (other == null) return false;
            if (Cas == other.Cas &&
                LookupCommands.Equals(other.LookupCommands) &&
                Key == other.Key)
            {
                return true;
            }
            return false;
        }
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
