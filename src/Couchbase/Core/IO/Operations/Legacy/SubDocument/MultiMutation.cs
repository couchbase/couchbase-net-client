using System;
using System.Collections.Generic;
using System.Text;
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

        public override byte[] Write()
        {
            var keyBytes = CreateKey();
            var totalLength = OperationHeader.Length + keyBytes.Length + BodyLength;
            var buffer = new byte[totalLength];

            WriteHeader(buffer);
            Buffer.BlockCopy(keyBytes, 0, buffer, OperationHeader.Length, keyBytes.Length);
            WriteBody(buffer, OperationHeader.Length + keyBytes.Length);
            return buffer;
        }

        public override byte[] CreateFramingExtras()
        {
            if (DurabilityLevel == DurabilityLevel.None)
            {
                return Array.Empty<byte>();
            }

            // TODO: omit timeout bytes if no timeout provided
            var bytes = new byte[2];

            var framingExtra = new FramingExtraInfo(RequestFramingExtraType.DurabilityRequirements, (byte) (bytes.Length - 1));
            Converter.FromByte(framingExtra.Byte, bytes, 0);
            Converter.FromByte((byte) DurabilityLevel, bytes, 1);

            // TODO: improve timeout, coerce to 1500ms, etc
            //var timeout = DurabilityTimeout.HasValue ? DurabilityTimeout.Value.TotalMilliseconds : 0;
            //Converter.FromUInt16((ushort)timeout, bytes, 2);

            return bytes;
        }

        public override void WriteHeader(byte[] buffer)
        {
            var keyBytes = CreateKey();
            var framingExtras = CreateFramingExtras();

            if (framingExtras.Length > 0)
            {
                Converter.FromByte((byte) Magic.AltRequest, buffer, HeaderOffsets.Magic); //0
                Converter.FromByte((byte) OpCode, buffer, HeaderOffsets.Opcode); //1
                Converter.FromByte((byte) framingExtras.Length, buffer, HeaderOffsets.KeyLength); //2
                Converter.FromByte((byte) keyBytes.Length, buffer, HeaderOffsets.AltKeyLength); //3
                Converter.FromByte((byte) ExtrasLength, buffer, HeaderOffsets.ExtrasLength); //4
            }
            else
            {
                Converter.FromByte((byte) Magic.Request, buffer, HeaderOffsets.Magic); //0
                Converter.FromByte((byte) OpCode, buffer, HeaderOffsets.Opcode); //1
                Converter.FromInt16((short) keyBytes.Length, buffer, HeaderOffsets.KeyLength); //2-3
                Converter.FromByte((byte) ExtrasLength, buffer, HeaderOffsets.ExtrasLength); //4
            }

            //5 datatype?
            if (VBucketId.HasValue)
            {
                Converter.FromInt16(VBucketId.Value, buffer, HeaderOffsets.VBucket);//6-7
            }

            Converter.FromInt32(framingExtras.Length + ExtrasLength + keyBytes.Length + BodyLength, buffer, HeaderOffsets.BodyLength);//8-11
            Converter.FromUInt32(Opaque, buffer, HeaderOffsets.Opaque);//12-15
            Converter.FromUInt64(Cas, buffer, HeaderOffsets.Cas);
        }

        public override byte[] CreateBody()
        {
            var buffer = new List<byte>();
            foreach (var mutate in Builder)
            {
                var opcode = (byte)mutate.OpCode;
                var flags = (byte) mutate.PathFlags;
                var pathLength = Encoding.UTF8.GetByteCount(mutate.Path);
                var fragment = mutate.Value == null ? Array.Empty<byte>() : GetBytes(mutate);

                var spec = new byte[pathLength + 8];
                Converter.FromByte(opcode, spec, 0);
                Converter.FromByte(flags, spec, 1);
                Converter.FromUInt16((ushort)pathLength, spec, 2);
                Converter.FromUInt32((uint)fragment.Length, spec, 4);
                Converter.FromString(mutate.Path, spec, 8);

                buffer.AddRange(spec);
                buffer.AddRange(fragment);
                _lookupCommands.Add(mutate);
            }
            return buffer.ToArray();
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
                    Data.Dispose();
                    Data = null;
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
                if (Data != null && !result.IsNmv())
                {
                    Data.Dispose();
                }
            }

            return result;
        }

        public override void ReadExtras(byte[] buffer)
        {
            TryReadMutationToken(buffer);
        }

        public IList<OperationSpec> GetCommandValues()
        {
            var response = Data.ToArray();
            ReadExtras(response);

            //all mutations successful
            if (response.Length == OperationHeader.Length + Header.FramingExtrasLength)
            {
                return _lookupCommands;
            }

            var indexOffset = Header.ExtrasOffset;
            var statusOffset = indexOffset + 1;
            var valueLengthOffset = indexOffset + 3;
            var valueOffset = indexOffset + 7;

            for (;;)
            {
                var index = Converter.ToByte(response, indexOffset);
                var command = _lookupCommands[index];
                command.Status = (ResponseStatus) Converter.ToUInt16(response, statusOffset);

                //if succcess read value and loop to next result - otherwise terminate loop here
                if (command.Status == ResponseStatus.Success)
                {
                    var valueLength = Converter.ToInt32(response, valueLengthOffset);
                    if (valueLength > 0)
                    {
                        var payLoad = new byte[valueLength];
                        System.Buffer.BlockCopy(response, valueOffset, payLoad, 0, valueLength);
                        command.Bytes = payLoad;
                    }
                    indexOffset = valueOffset + valueLength;
                    statusOffset = indexOffset + 1;
                    valueLengthOffset = indexOffset + 3;
                    valueOffset = indexOffset + 7;
                }

                if (valueOffset + Header.ExtrasLength > response.Length) break;
            }
            return _lookupCommands;
        }

        public override short ExtrasLength
        {
            get { return (short)(Expires == 0 ? 0 : 4); }
        }

        public override void WriteKey(byte[] buffer, int offset)
        {
            Converter.FromString(Key, buffer, offset);
        }

        public override void WriteBody(byte[] buffer, int offset)
        {
            System.Buffer.BlockCopy(BodyBytes, 0, buffer, offset, BodyLength);
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
