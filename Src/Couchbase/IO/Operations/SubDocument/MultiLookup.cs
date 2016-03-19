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
    internal class MultiLookup<T> : OperationBase<T>
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
            var totalLength = HeaderLength + KeyLength + BodyLength;
            var buffer = AllocateBuffer(totalLength);

            WriteHeader(buffer);
            WriteKey(buffer, HeaderLength);
            WriteBody(buffer, HeaderLength + KeyLength);
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
                var opcode = (byte)lookup.OpCode;
                var flags = new byte();//empty for lookups
                var pathLength = Encoding.UTF8.GetByteCount(lookup.Path);

                var spec = new byte[pathLength + 4];
                Converter.FromByte(opcode, spec, 0);
                Converter.FromByte(flags, spec, 1);
                Converter.FromUInt16((ushort)pathLength, spec, 2);
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

        public override T GetValue()
        {
            var response = Data.ToArray();
            var statusOffset = 24;
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
                command.ValueIsJson = payLoad.IsJson(0, bodyLength);
                command.Bytes = payLoad;

                statusOffset = valueOffset + bodyLength;
                valueLengthOffset = statusOffset + 2;
                valueOffset = statusOffset + 6;

                if (valueOffset >= response.Length) break;
            }
            return (T)_lookupCommands;
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
                result.Value = (IList<OperationSpec>) GetValue();

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
    }
}
