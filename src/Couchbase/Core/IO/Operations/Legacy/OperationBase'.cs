using System;
using System.Reflection;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal abstract class OperationBase<T> : OperationBase, IOperation<T>
    {
        public T Content { get; set; }

        public override byte[] CreateBody()
        {
            byte[] bytes;
            if (typeof(T).GetTypeInfo().IsValueType)
            {
                bytes = Transcoder.Encode(Content, Flags, OpCode);
            }
            else
            {
                bytes = Content == null ? Array.Empty<byte>() :
                    Transcoder.Encode(Content, Flags, OpCode);
            }

            return bytes;
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


        public virtual T GetValue()
        {
            var result = default(T);
            if(Success && Data != null && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.ToArray().AsMemory();
                    ReadExtras(buffer.Span);
                    var offset = Header.BodyOffset;
                    var length = Header.TotalLength - Header.BodyOffset;
                    result = Transcoder.Decode<T>(buffer.Slice(offset, length), Flags, OpCode);
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[8];

            Flags = Transcoder.GetFormat(Content);
            Format = Flags.DataFormat;
            Compression = Flags.Compression;

            byte format = (byte)Format;
            byte compression = (byte)Compression;

            Converter.SetBit(ref extras[0], 0, Converter.GetBit(format, 0));
            Converter.SetBit(ref extras[0], 1, Converter.GetBit(format, 1));
            Converter.SetBit(ref extras[0], 2, Converter.GetBit(format, 2));
            Converter.SetBit(ref extras[0], 3, Converter.GetBit(format, 3));
            Converter.SetBit(ref extras[0], 4, false);
            Converter.SetBit(ref extras[0], 5, Converter.GetBit(compression, 0));
            Converter.SetBit(ref extras[0], 6, Converter.GetBit(compression, 1));
            Converter.SetBit(ref extras[0], 7, Converter.GetBit(compression, 2));

            var typeCode = (ushort)Flags.TypeCode;
            Converter.FromUInt16(typeCode, extras, 2);
            Converter.FromUInt32(Expires, extras, 4);

            return extras;
        }

        public override byte[] Write()
        {
            var extras = CreateExtras();
            var key = CreateKey();
            var body = CreateBody();
            var framingExtras = CreateFramingExtras();
            var header = CreateHeader(extras, body, key, framingExtras);

            var buffer = new byte[extras.GetLengthSafe() +
                                  body.GetLengthSafe() +
                                  key.GetLengthSafe() +
                                  header.GetLengthSafe() +
                                  framingExtras.GetLengthSafe()];

            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(framingExtras, 0, buffer, header.Length, framingExtras.Length);
            Buffer.BlockCopy(extras, 0, buffer, header.Length + framingExtras.Length, extras.Length);
            Buffer.BlockCopy(key, 0, buffer, header.Length + framingExtras.Length + extras.Length, key.Length);
            Buffer.BlockCopy(body, 0, buffer, header.Length + framingExtras.Length + extras.Length + key.Length, body.Length);

            return buffer;
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
