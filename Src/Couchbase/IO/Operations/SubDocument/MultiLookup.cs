using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class MultiLookup<T> : OperationBase<T>, IEquatable<MultiLookup<T>>
    {
        private readonly LookupInBuilder<T> _builder;
        private readonly IList<OperationSpec> _lookupCommands = new List<OperationSpec>();

        public MultiLookup(string key, LookupInBuilder<T> builder, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            _builder = builder;
        }

        public override byte[] Write()
        {
            var totalLength = OperationHeader.Length + KeyLength + BodyLength;
            var buffer = AllocateBuffer(totalLength);

            WriteHeader(buffer);
            WriteKey(buffer, OperationHeader.Length);
            WriteBody(buffer, OperationHeader.Length + KeyLength);
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
            foreach (var lookup in _builder)
            {
                var opcode = (byte) lookup.OpCode;
                var flags = (byte) lookup.PathFlags;
                var pathLength = Encoding.UTF8.GetByteCount(lookup.Path);

                var spec = new byte[pathLength + 4];
                Converter.FromByte(opcode, spec, 0);
                Converter.FromByte(flags, spec, 1);
                Converter.FromUInt16((ushort) pathLength, spec, 2);
                Converter.FromString(lookup.Path, spec, 4);

                buffer.AddRange(spec);
                _lookupCommands.Add(lookup);
            }
            return buffer.ToArray();
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
            get { return OperationCode.MultiLookup; }
        }

        public IList<OperationSpec> GetMultiValues()
        {
            //Fix for NCBC-2179 Do not attempt to parse body if response is not my vbucket
            if (Header.Status == ResponseStatus.VBucketBelongsToAnotherServer)
                return null;

            var response = Data.ToArray();
            var statusOffset = Header.BodyOffset;
            var valueLengthOffset = statusOffset + 2;
            var valueOffset = statusOffset + 6;
            var commandIndex = 0;

            for (;;)
            {
                var bodyLength = Converter.ToInt32(response, valueLengthOffset);
                var payLoad = new byte[bodyLength];
                System.Buffer.BlockCopy(response, valueOffset, payLoad, 0, bodyLength);

                var command = _lookupCommands[commandIndex++];
                command.Status = (ResponseStatus)Converter.ToUInt16(response, statusOffset);
                command.ValueIsJson = payLoad.IsJson(0, bodyLength - 1);
                command.Bytes = payLoad;
                statusOffset = valueOffset + bodyLength;
                valueLengthOffset = statusOffset + 2;
                valueOffset = statusOffset + 6;

                if (valueOffset > response.Length) break;
            }

            return _lookupCommands;
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
                result.Value = GetMultiValues();

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

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new MultiLookup<T>(Key, (LookupInBuilder<T>)_builder.Clone(), VBucket, Transcoder, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
        }

        /// <summary>
        /// Determines whether this instance can be retried.
        /// </summary>
        /// <returns></returns>
        public override bool CanRetry()
        {
            return ErrorCode == null || ErrorMapRequestsRetry();
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(MultiLookup<T> other)
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
