using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class MultiMutation<T> : OperationBase<T>, IEquatable<MultiMutation<T>>
    {
        private readonly MutateInBuilder<T> _builder;
        private readonly IList<OperationSpec> _mutateCommands = new List<OperationSpec>();

        public MultiMutation(string key, MutateInBuilder<T> mutateInBuilder, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            _builder = mutateInBuilder;
            Cas = _builder.Cas;
        }

        public override byte[] Write()
        {
            // create this first because we need to iterate builder for operation specs
            var bodyBytes = CreateBody();

            var totalLength = OperationHeader.Length + KeyLength + ExtrasLength + bodyBytes.Length;
            var buffer = AllocateBuffer(totalLength);

            WriteHeader(buffer);
            WriteExtras(buffer, OperationHeader.Length);
            WriteKey(buffer, OperationHeader.Length + ExtrasLength);

            // manually write body to buffer so we won't re-create it
            System.Buffer.BlockCopy(bodyBytes, 0, buffer, OperationHeader.Length + ExtrasLength + KeyLength, bodyBytes.Length);
            //WriteBody(buffer, OperationHeader.Length + ExtrasLength + KeyLength);
            return buffer;
        }

        public override void WriteHeader(byte[] buffer)
        {
            Converter.FromByte((byte)Magic.Request, buffer, HeaderIndexFor.Magic);//0
            Converter.FromByte((byte)OperationCode, buffer, HeaderIndexFor.Opcode);//1
            Converter.FromInt16(KeyLength, buffer, HeaderIndexFor.KeyLength);//2-3
            Converter.FromByte((byte)ExtrasLength, buffer, HeaderIndexFor.ExtrasLength);  //4
            //5 datatype?
            if (VBucket != null)
            {
                Converter.FromInt16((short)VBucket.Index, buffer, HeaderIndexFor.VBucket);//6-7
            }

            Converter.FromInt32(ExtrasLength + KeyLength + BodyLength, buffer, HeaderIndexFor.BodyLength);//8-11
            Converter.FromUInt32(Opaque, buffer, HeaderIndexFor.Opaque);//12-15
            Converter.FromUInt64(Cas, buffer, HeaderIndexFor.Cas);
        }

        public override byte[] CreateBody()
        {
            var buffer = new List<byte>();
            foreach (var mutate in _builder)
            {
                var opcode = (byte)mutate.OpCode;
                var flags = (byte) mutate.PathFlags;
                var pathLength = Encoding.UTF8.GetByteCount(mutate.Path);
                var fragment = mutate.Value == null ? new byte[0] : GetBytes(mutate);

                var spec = new byte[pathLength + 8];
                Converter.FromByte(opcode, spec, 0);
                Converter.FromByte(flags, spec, 1);
                Converter.FromUInt16((ushort)pathLength, spec, 2);
                Converter.FromUInt32((uint)fragment.Length, spec, 4);
                Converter.FromString(mutate.Path, spec, 8);

                buffer.AddRange(spec);
                buffer.AddRange(fragment);
                _mutateCommands.Add(mutate);
            }
            return buffer.ToArray();
        }

        public override void WriteExtras(byte[] buffer, int offset)
        {
            var hasExpiry = Expires > 0;
            if (hasExpiry)
            {
                Converter.FromUInt32(Expires, buffer, offset); //4 Expiration time
            }

            if (_mutateCommands.Any(spec => spec.DocFlags != SubdocDocFlags.None))
            {
                var docFlags = SubdocDocFlags.None;
                foreach (var spec in _mutateCommands)
                {
                    docFlags |= spec.DocFlags;
                }

                // write doc flags, offset depends on if there is an expiry
                Converter.FromByte((byte) docFlags, buffer, offset + (hasExpiry ? 4 : 0));
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
            var result = new DocumentFragment<T>(_builder);
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
                return _mutateCommands;
            }

            var indexOffset = Header.ExtrasOffset;
            var statusOffset = indexOffset + 1;
            var valueLengthOffset = indexOffset + 3;
            var valueOffset = indexOffset + 7;

            for (;;)
            {
                var index = Converter.ToByte(response, indexOffset);
                var command = _mutateCommands[index];
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
            return _mutateCommands;
        }

        private short? _extrasLength;
        public override short ExtrasLength
        {
            get
            {
                if (!_extrasLength.HasValue)
                {
                    short length = 0;
                    if (Expires > 0)
                    {
                        length += 4;
                    }

                    if (_mutateCommands.Any(x => x.DocFlags != SubdocDocFlags.None))
                    {
                        length += 1;
                    }

                    _extrasLength = length;
                }

                return _extrasLength.Value;
            }
        }

        public override void WriteKey(byte[] buffer, int offset)
        {
            Converter.FromString(Key, buffer, offset);
        }

        public override void WriteBody(byte[] buffer, int offset)
        {
            System.Buffer.BlockCopy(BodyBytes, 0, buffer, offset, BodyLength);
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubMultiMutation; }
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new MultiMutation<T>(Key, (MutateInBuilder<T>)_builder.Clone(), VBucket, Transcoder, Timeout)
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
                _builder.Equals(other._builder) &&
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
