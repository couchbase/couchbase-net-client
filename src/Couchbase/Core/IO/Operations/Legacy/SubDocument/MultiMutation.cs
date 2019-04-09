using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    internal class MultiMutation<T> : OperationBase<T>, IEquatable<MultiMutation<T>>
    {
        public MutateInBuilder<T> Builder { get; set; }
        private readonly IList<OperationSpec> _lookupCommands = new List<OperationSpec>();

        public DurabilityLevel DurabilityLevel { get; set; }
        public TimeSpan? DurabilityTimeout { get; set; }

        public override void WriteExtras(OperationBuilder builder)
        {
            if (Expires <= 0)
            {
                return;
            }

            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            Converter.FromUInt32(Expires, buffer);
            builder.Write(buffer);
        }

        public override void WriteFramingExtras(OperationBuilder builder)
        {
            if (DurabilityLevel == DurabilityLevel.None)
            {
                return;
            }

            // TODO: omit timeout bytes if no timeout provided
            Span<byte> bytes = stackalloc byte[2];

            var framingExtra = new FramingExtraInfo(RequestFramingExtraType.DurabilityRequirements, (byte) (bytes.Length - 1));
            Converter.FromByte(framingExtra.Byte, bytes);
            Converter.FromByte((byte) DurabilityLevel, bytes.Slice(1));

            // TODO: improve timeout, coerce to 1500ms, etc
            //var timeout = DurabilityTimeout.HasValue ? DurabilityTimeout.Value.TotalMilliseconds : 0;
            //Converter.FromUInt16((ushort)timeout, bytes, 2);

            builder.Write(bytes);
        }

        public override void WriteBody(OperationBuilder builder)
        {
            using (var bufferOwner = MemoryPool<byte>.Shared.Rent(OperationSpec.MaxPathLength + 8))
            {
                var buffer = bufferOwner.Memory.Span;
                foreach (var mutate in Builder)
                {
                    var pathLength = Converter.FromString(mutate.Path, buffer.Slice(8));

                    var opcode = (byte) mutate.OpCode;
                    var flags = (byte) mutate.PathFlags;
                    var fragment = mutate.Value == null ? Array.Empty<byte>() : GetBytes(mutate);

                    Converter.FromByte(opcode, buffer);
                    Converter.FromByte(flags, buffer.Slice(1));
                    Converter.FromUInt16((ushort) pathLength, buffer.Slice(2));
                    Converter.FromUInt32((uint) fragment.Length, buffer.Slice(4));

                    // TODO: Optimize fragment handling to use OperationBuilder directly
                    builder.Write(buffer.Slice(0, pathLength + 8));
                    builder.Write(fragment, 0, fragment.Length);
                    _lookupCommands.Add(mutate);
                }
            }
        }

        byte[] GetBytes(OperationSpec spec)
        {
            var bytes = Transcoder.Serializer.Serialize(spec.Value);
            if (spec.RemoveBrackets)
            {
                return bytes.StripBrackets();
            }
            return bytes;
        }

        public override IOperationResult<T> GetResultWithValue()
        {
            var result = new DocumentFragment<T>(Builder);
            try
            {
                result.Success = GetSuccess();
                result.Message = GetMessage();
                result.Status = GetResponseStatus();
                result.Cas = Header.Cas;
                result.Exception = Exception;
                result.Token = MutationToken ?? DefaultMutationToken;
                result.Value = GetCommandValues();

                //clean up and set to null
                if (!result.IsNmv())
                {
                    Dispose();
                }
            }
            catch (Exception e)
            {
                result.Exception = e;
                result.Success = false;
                result.Status = ResponseStatus.ClientFailure;
            }
            finally
            {
                if (!result.IsNmv())
                {
                    Dispose();
                }
            }

            return result;
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            TryReadMutationToken(buffer);
        }

        public IList<OperationSpec> GetCommandValues()
        {
            var responseSpan = Data.Span;
            ReadExtras(responseSpan);

            //all mutations successful
            if (responseSpan.Length == OperationHeader.Length + Header.FramingExtrasLength)
            {
                return _lookupCommands;
            }

            responseSpan = responseSpan.Slice(Header.ExtrasOffset);

            for (;;)
            {
                var index = Converter.ToByte(responseSpan);
                var command = _lookupCommands[index];
                command.Status = (ResponseStatus) Converter.ToUInt16(responseSpan.Slice(1));

                //if success read value and loop to next result - otherwise terminate loop here
                if (command.Status == ResponseStatus.Success)
                {
                    var valueLength = Converter.ToInt32(responseSpan.Slice(3));
                    if (valueLength > 0)
                    {
                        var payLoad = new byte[valueLength];
                        responseSpan.Slice(7, valueLength).CopyTo(payLoad);
                        command.Bytes = payLoad;
                    }

                    responseSpan = responseSpan.Slice(7 + valueLength);
                }

                if (responseSpan.Length <= 0) break;
            }
            return _lookupCommands;
        }

        public override OpCode OpCode
        {
            get { return OpCode.SubMultiMutation; }
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new MultiMutation<T>
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode,
                Expires = Expires
            };
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(MultiMutation<T> other)
        {
            if (other == null) return false;
            if (Cas == other.Cas &&
                Builder.Equals(other.Builder) &&
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
