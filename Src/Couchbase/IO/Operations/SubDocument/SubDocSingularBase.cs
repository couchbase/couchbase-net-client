using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularBase<T> : OperationBase<T>
    {
        private short _pathLength;
        protected ITypeSerializerProvider Builder;
        protected OperationSpec CurrentSpec;

        protected SubDocSingularBase(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, default(T), vBucket, transcoder, opaque, timeout)
        {
            Builder = builder;
        }

        protected SubDocSingularBase(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            Builder = builder;
        }

        public string Path { get; protected set; }

        public override short PathLength
        {
            get
            {
                if (_pathLength == 0)
                {
                    _pathLength = (short)Encoding.UTF8.GetByteCount(Path);
                }
                return _pathLength;
            }
        }

        public override byte[] AllocateBuffer(int length)
        {
            return new byte[length];
        }

        public override short ExtrasLength
        {
            get { return (short)(Expires == 0 ? 3 : 7); }
        }

        protected virtual ResponseStatus GetParentStatus(ResponseStatus status)
        {
            switch (status)
            {
                case ResponseStatus.None:
                case ResponseStatus.Success:
                case ResponseStatus.KeyNotFound:
                case ResponseStatus.KeyExists:
                case ResponseStatus.ValueTooLarge:
                case ResponseStatus.InvalidArguments:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                case ResponseStatus.VBucketBelongsToAnotherServer:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue:
                case ResponseStatus.InvalidRange:
                case ResponseStatus.UnknownCommand:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.NotSupported:
                case ResponseStatus.InternalError:
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.ClientFailure:
                case ResponseStatus.OperationTimeout:
                case ResponseStatus.NoReplicasFound:
                case ResponseStatus.NodeUnavailable:
                case ResponseStatus.TransportFailure:
                    break;
                case ResponseStatus.DocumentMutationLost:
                    break;
                case ResponseStatus.SubDocPathNotFound:
                case ResponseStatus.SubDocPathMismatch:
                case ResponseStatus.SubDocPathInvalid:
                case ResponseStatus.SubDocPathTooBig:
                case ResponseStatus.SubDocDocTooDeep:
                case ResponseStatus.SubDocPathExists:
                    return ResponseStatus.SubDocMultiPathFailure;
                case ResponseStatus.SubDocCannotInsert:
                case ResponseStatus.SubDocDocNotJson:
                case ResponseStatus.SubDocNumRange:
                case ResponseStatus.SubDocDeltaRange:
                case ResponseStatus.SubDocValueTooDeep:
                case ResponseStatus.SubDocInvalidCombo:
                    break;
                case ResponseStatus.SubDocMultiPathFailure:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return status;
        }

        public override IOperationResult<T> GetResultWithValue()
        {
            var result = new DocumentFragment<T>(Builder);
            try
            {
                var status = GetResponseStatus();
                result.Success = GetSuccess();
                result.Message = GetMessage();
                result.Status = GetParentStatus(status);
                result.Cas = Header.Cas;
                result.Exception = Exception;
                result.Token = MutationToken ?? DefaultMutationToken;

                CurrentSpec.Value = GetValue();
                CurrentSpec.Status = status;
                result.Value = new List<OperationSpec> { CurrentSpec };

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

        public override void WriteKey(byte[] buffer, int offset)
        {
            Converter.FromString(Key, buffer, offset);
        }

        public override void WritePath(byte[] buffer, int offset)
        {
            Converter.FromString(Path, buffer, offset);
        }

        public override void WriteBody(byte[] buffer, int offset)
        {
            System.Buffer.BlockCopy(BodyBytes, 0, buffer, offset, BodyLength);
        }

        public override T GetValue()
        {
            var result = default(T);
            if (Success && Data != null && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.ToArray();
                    ReadExtras(buffer);
                    var offset = 24 + Header.KeyLength + Header.ExtrasLength;
                    CurrentSpec.ValueIsJson = buffer.IsJson(offset, TotalLength-1);
                    CurrentSpec.Bytes = new byte[TotalLength-offset];
                    System.Buffer.BlockCopy(buffer, offset, CurrentSpec.Bytes, 0, TotalLength-offset);
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }
    }
}
