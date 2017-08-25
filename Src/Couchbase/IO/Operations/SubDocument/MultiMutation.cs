using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class MultiMutation<T> : OperationBase<T>, IEquatable<MultiMutation<T>>
    {
        private readonly MutateInBuilder<T> _builder;
        private readonly IList<OperationSpec> _lookupCommands = new List<OperationSpec>();

        public MultiMutation(string key, MutateInBuilder<T> mutateInBuilder, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            _builder = mutateInBuilder;
            Cas = _builder.Cas;
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
            if (buffer.Length >= 40 && VBucket != null)
            {
                var uuid = Converter.ToInt64(buffer, 24);
                var seqno = Converter.ToInt64(buffer, 32);
                MutationToken = new MutationToken(VBucket.BucketName, (short)VBucket.Index, uuid, seqno);
            }
        }

        public IList<OperationSpec> GetCommandValues()
        {
            var response = Data.ToArray();
            ReadExtras(response);

            //all mutations successful
            if(response.Length == HeaderLength) return _lookupCommands;

            var indexOffset = 24;
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
