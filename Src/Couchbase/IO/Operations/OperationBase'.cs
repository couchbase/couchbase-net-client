using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;
using System;
using System.Reflection;

namespace Couchbase.IO.Operations
{
    internal abstract class OperationBase<T> : OperationBase, IOperation<T>
    {
        protected T _value;

        protected OperationBase(string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, vBucket, transcoder, opaque, timeout)
        {
            _value = value;
        }

        protected OperationBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : this(key, default(T), vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
        }

        public override byte[] CreateBody()
        {
            byte[] bytes;
            if (typeof(T).GetTypeInfo().IsValueType)
            {
                bytes = Transcoder.Encode(RawValue, Flags, OperationCode);
            }
            else
            {
                bytes = RawValue == null ? new byte[0] :
                    Transcoder.Encode(RawValue, Flags, OperationCode);
            }

            return bytes;
        }
        
        public Couchbase.IOperationResult<T> GetResultWithValue()
        {
            var result = new OperationResult<T>();
            try
            {
                var value = GetValue();
                result.Success = GetSuccess();
                result.Message = GetMessage();
                result.Status = GetResponseStatus();
                result.Value = value;
                result.Cas = Header.Cas;
                result.Exception = Exception;
                result.Token = MutationToken ?? DefaultMutationToken;

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
                    var buffer = Data.ToArray();
                    ReadExtras(buffer);
                    var offset = 24 + Header.KeyLength + Header.ExtrasLength;
                    result = Transcoder.Decode<T>(buffer, offset, TotalLength - offset, Flags, OperationCode);
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }

        internal T RawValue
        {
            get { return _value; }
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[8];

            Flags = Transcoder.GetFormat<T>(RawValue);
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
            var header = CreateHeader(extras, body, key);

            var buffer = new byte[extras.GetLengthSafe() +
                                  body.GetLengthSafe() +
                                  key.GetLengthSafe() +
                                  header.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length + extras.Length + key.Length, body.Length);

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
