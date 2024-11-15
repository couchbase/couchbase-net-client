using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Converters;
using Couchbase.KeyValue;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations.SubDocument
{
    internal sealed class MultiMutation<T> : OperationBase<T>, IEquatable<MultiMutation<T>>
    {
        public ReadOnlyCollection<MutateInSpec> MutateCommands { get; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? DurabilityTimeout { get; set; }

        public SubdocDocFlags DocFlags { get; set; }

        // Optionally, we can set the user flags on the doc.  This should only
        // be used in servers that support ReplaceBodyWithXattr.
        public Flags? OptionalFlags { get; set; }


        /// <inheritdoc />
        public override bool IsReadOnly => false;

        public MultiMutation(string key, IEnumerable<MutateInSpec> specs)
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

            MutateCommands = new ReadOnlyCollection<MutateInSpec>(commands);
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
            // Lets get 1 buffer to hold everything. Given its max size is just 9
            // bytes, lets just get that each time.
            Span<byte> buffer = builder.GetSpan(sizeof(uint) + Flags.Size + sizeof(byte));
            var offset = 0;

            // If you are setting the OptionalFlags, you MUST set an
            // expiry, even if it is 0
            if (Expires > 0 || OptionalFlags != null)
            {
                ByteConverter.FromUInt32(Expires, buffer);
                offset += sizeof(uint);
            }
            if (OptionalFlags != null)
            {
                OptionalFlags.Value.Write(buffer.Slice(offset));
                offset += Flags.Size;
            }
            if (DocFlags != SubdocDocFlags.None)
            {
                //Add the doc flags
                buffer[offset] = (byte)DocFlags;
                offset++;
            }

            builder.Advance(offset);
        }

        protected override void WriteFramingExtras(OperationBuilder builder)
        {
            if (PreserveTtl)
            {
                builder.WriteByte(5 << 4);
            }

            if (DurabilityLevel == DurabilityLevel.None)
            {
                return;
            }

            // TODO: omit timeout bytes if no timeout provided

            var framingExtra = new FramingExtraInfo(RequestFramingExtraType.DurabilityRequirements, length: 1);
            var bytes = builder.GetSpan(framingExtra.Length + 1);
            bytes[0] = framingExtra.Byte;
            bytes[1] = (byte) DurabilityLevel;

            // TODO: improve timeout, coerce to 1500ms, etc
            //var timeout = DurabilityTimeout.HasValue ? DurabilityTimeout.Value.TotalMilliseconds : 0;
            //Converter.FromUInt16((ushort)timeout, bytes, 2);

            builder.Advance(framingExtra.Length + 1);
        }

        protected override void WriteBody(OperationBuilder builder)
        {
            try
            {
                foreach (var mutate in MutateCommands)
                {
                    builder.BeginOperationSpec(true);

                    var bufferSpan = builder.GetSpan(OperationSpec.MaxPathLength);

                    var pathLength = ByteConverter.FromString(mutate.Path, bufferSpan);
                    builder.Advance(pathLength);

                    if (mutate.OpCode is not OpCode.SubDelete and not OpCode.Delete and not OpCode.SubReplaceBodyWithXattr)
                    {
                        builder.AdvanceToSegment(OperationSegment.OperationSpecFragment);
                        mutate.WriteSpecValue(builder, Transcoder);
                    }

                    builder.CompleteOperationSpec(mutate);
                }
            }
            catch (ArgumentException e)
            {
                throw new InvalidArgumentException("The Path is invalid.", e);
            }
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            TryReadMutationToken(buffer);

            TryReadServerDuration(buffer);
        }

        /// <summary>
        /// Parses the response data into <see cref="MutateCommands"/>.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> to cleanup any data buffers.</returns>
        /// <remarks>
        /// The parsed <see cref="OperationSpec.Bytes"/> is a reference to the memory in the response data.
        /// It is no longer valid once the response data is disposed via the returned <see cref="IDisposable"/>.
        /// </remarks>
        public IDisposable ParseCommandValues()
        {
            var headerSpan = Data.Span;
            ReadExtras(headerSpan);

            //all mutations successful
            if (headerSpan.Length == OperationHeader.Length + Header.FramingExtrasLength)
            {
                return NullDisposable.Instance;
            }

            if (Header.BodyOffset > headerSpan.Length)
            {
                throw new DecodingFailureException();
            }

            var data = ExtractBody();
            try
            {
                //some commands return nothing - so return back an empty list
                if (data.Memory.Length == 0)
                {
                    data.Dispose();
                    return NullDisposable.Instance;
                }

                var responseSegment = data.Memory;
                for (;;)
                {
                    var index = responseSegment.Span[0];
                    var command = MutateCommands[index];
                    command.Status = (ResponseStatus) ByteConverter.ToUInt16(responseSegment.Span.Slice(1));

                    //if success read value and loop to next result - otherwise terminate loop here
                    if (command.Status == ResponseStatus.Success)
                    {
                        var valueLength = ByteConverter.ToInt32(responseSegment.Span.Slice(3));
                        command.Bytes = valueLength > 0
                            ? responseSegment.Slice(7, valueLength)
                            : default;

                        responseSegment = responseSegment.Slice(7 + valueLength);
                    }
                    else
                    {
                        break;
                    }

                    if (responseSegment.Length <= 0) break;
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

        public override OpCode OpCode => OpCode.SubMultiMutation;

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(MultiMutation<T>? other)
        {
            if (other == null) return false;
            if (Cas == other.Cas &&
                MutateCommands.Equals(other.MutateCommands) &&
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
