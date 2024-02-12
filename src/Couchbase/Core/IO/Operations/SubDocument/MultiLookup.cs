using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Converters;
using Couchbase.KeyValue;
using Couchbase.Query.Couchbase.N1QL;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations.SubDocument
{
    internal class MultiLookup<T> : OperationBase<T>, IEquatable<MultiLookup<T>>
    {
        public ReadOnlyCollection<LookupInSpec> LookupCommands { get; }
        public SubdocDocFlags DocFlags { get; set; }

        public MultiLookup(string key, IEnumerable<LookupInSpec> specs, short? replicaIndex = null)
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
            ReplicaIdx = replicaIndex;
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
                    if (lookup.Path.Length > OperationSpec.MaxPathLength)
                    {
                        throw new InvalidArgumentException(
                            $"Path length of {lookup.Path.Length} exceeds maximum ({OperationSpec.MaxPathLength}");
                    }

                    var pathLength = 0;
                    try
                    {
                        pathLength = ByteConverter.FromString(lookup.Path, bufferSpan);
                    }
                    catch (ArgumentException e)
                    {
                        // the preceding length check will catch most situations, but the "Length" of a UTF8 string does
                        // not necessarily correspond to its byte encoding length.
                        throw new InvalidArgumentException("Path is invalid.", e);
                    }

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

        /// <summary>
        /// Parses the response data into <see cref="LookupCommands"/>.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> to cleanup any data buffers.</returns>
        /// <remarks>
        /// The parsed <see cref="OperationSpec.Bytes"/> is a reference to the memory in the response data.
        /// It is no longer valid once the response data is disposed via the returned <see cref="IDisposable"/>.
        /// </remarks>
        public IDisposable ParseCommandValues()
        {
            if (Data.IsEmpty)
            {
                return NullDisposable.Instance;
            }

            var data = ExtractBody();
            try
            {
                var responseSegment = data.Memory;
                foreach (var command in LookupCommands)
                {
                    var bodyLength = ByteConverter.ToInt32(responseSegment.Span.Slice(2));

                    command.Status = (ResponseStatus)ByteConverter.ToUInt16(responseSegment.Span);

                    var payload = responseSegment.Slice(6, bodyLength);
                    command.ValueIsJson = payload.Span.IsJson();
                    command.Bytes = payload;

                    responseSegment = responseSegment.Slice(6 + bodyLength);
                    if (responseSegment.Length <= 0)
                    {
                        break;
                    }
                }

                return data;
            }
            catch
            {
                // Dispose the data buffer if an exception is thrown
                data.Dispose();
                throw;
            }
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
