using System;
using System.Linq;
using System.Reflection;
using Couchbase.Core.IO.Converters;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations
{
    internal abstract class OperationBase<T> : OperationBase, IOperation<T>
    {
        public T Content { get; set; }

        public override void WriteBody(OperationBuilder builder)
        {
            if (typeof(T).GetTypeInfo().IsValueType || Content != null)
            {
                Transcoder.Encode(builder, Content, Flags, OpCode);
            }
        }

        public virtual IOperationResult<T> GetResultWithValue()
        {
            var result = new OperationResult<T> {Id = Key};
            try
            {
                var value = GetValue();
                result.Success = GetSuccess();
                result.Message = GetMessage();
                result.Status = GetResponseStatus();
                result.Content = value;
                result.Cas = Header.Cas;
                result.Exception = Exception;
                result.Token = MutationToken ?? DefaultMutationToken;
                result.Id = Key;
                result.OpCode = OpCode;

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
                        using var decompressed = OperationCompressor.Decompress(buffer.Slice(offset, length));
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

        public override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> extras = stackalloc byte[8];

            Flags = Transcoder.GetFormat(Content);
            Format = Flags.DataFormat;
            Compression = Flags.Compression;

            byte format = (byte)Format;
            byte compression = (byte)Compression;

            BitUtils.SetBit(ref extras[0], 0, BitUtils.GetBit(format, 0));
            BitUtils.SetBit(ref extras[0], 1, BitUtils.GetBit(format, 1));
            BitUtils.SetBit(ref extras[0], 2, BitUtils.GetBit(format, 2));
            BitUtils.SetBit(ref extras[0], 3, BitUtils.GetBit(format, 3));
            BitUtils.SetBit(ref extras[0], 4, false);
            BitUtils.SetBit(ref extras[0], 5, BitUtils.GetBit(compression, 0));
            BitUtils.SetBit(ref extras[0], 6, BitUtils.GetBit(compression, 1));
            BitUtils.SetBit(ref extras[0], 7, BitUtils.GetBit(compression, 2));

            var typeCode = (ushort)Flags.TypeCode;
            ByteConverter.FromUInt16(typeCode, extras.Slice(2));
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
