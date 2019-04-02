using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    internal class MultiLookup<T> : OperationBase<T>, IEquatable<MultiLookup<T>>
    {
        public LookupInBuilder<T> Builder { get; set; }
        public readonly IList<OperationSpec> LookupCommands = new List<OperationSpec>();

        public override async Task SendAsync(IConnection connection)
        {
            var keyBytes = CreateKey();
            var totalLength = OperationHeader.Length + keyBytes.Length + BodyLength;
            var buffer = new byte[totalLength];

            WriteHeader(buffer);
            Buffer.BlockCopy(keyBytes, 0, buffer, OperationHeader.Length, keyBytes.Length);
            WriteBody(buffer, OperationHeader.Length + keyBytes.Length);

            await connection.SendAsync(buffer, Completed).ConfigureAwait(false);
        }

        public override void WriteHeader(byte[] buffer)
        {
            var keyBytes = CreateKey();
            Converter.FromByte((byte)Magic.Request, buffer, HeaderOffsets.Magic);//0
            Converter.FromByte((byte)OpCode, buffer, HeaderOffsets.Opcode);//1
            Converter.FromInt16((short)keyBytes.Length, buffer, HeaderOffsets.KeyLength);//2-3
            Converter.FromByte((byte)ExtrasLength, buffer, HeaderOffsets.ExtrasLength);  //4
            //5 datatype?
            if (VBucketId.HasValue)
            {
                Converter.FromInt16((short)VBucketId, buffer, HeaderOffsets.VBucket);//6-7
            }

            Converter.FromInt32(ExtrasLength + keyBytes.Length + BodyLength, buffer, HeaderOffsets.BodyLength);//8-11
            Converter.FromUInt32(Opaque, buffer, HeaderOffsets.Opaque);//12-15
            Converter.FromUInt64(Cas, buffer, HeaderOffsets.Cas);
        }

        public override byte[] CreateBody()
        {
            var buffer = new List<byte>();
            foreach (var lookup in Builder)
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
                LookupCommands.Add(lookup);
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

        public override OpCode OpCode => OpCode.MultiLookup;

        public override T GetValue()
        {
            var responseSpan = Data.Span.Slice(Header.BodyOffset);
            var commandIndex = 0;

            for (;;)
            {
                var bodyLength = Converter.ToInt32(responseSpan.Slice(2));
                var payLoad = new byte[bodyLength];
                responseSpan.Slice(6, bodyLength).CopyTo(payLoad);

                var command = LookupCommands[commandIndex++];
                command.Status = (ResponseStatus)Converter.ToUInt16(responseSpan);
                command.ValueIsJson = payLoad.AsSpan().IsJson();
                command.Bytes = payLoad;

                responseSpan = responseSpan.Slice(6 + bodyLength);
                if (responseSpan.Length <= 0) break;
            }
            return (T)LookupCommands;
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
                result.Value = (IList<OperationSpec>) GetValue();

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

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new MultiLookup<T>
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
