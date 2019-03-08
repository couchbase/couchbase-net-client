using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    internal abstract class SubDocSingularBase<T> : OperationBase<T>
    {
        private short _pathLength;
        protected ITypeSerializerProvider Builder;
        protected OperationSpec CurrentSpec;

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

        public override short ExtrasLength
        {
            get
            {
                short length = 3;
                if (Expires > 0)
                {
                    length += 4;
                }
                if (CurrentSpec.DocFlags != SubdocDocFlags.None)
                {
                    length += 1;
                }

                return length;
            }
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

                CurrentSpec.Value = GetValue();
                CurrentSpec.Status = status;
                result.Value = new List<OperationSpec> { CurrentSpec };

                // Read MutationToken after GetValue(), which may fill it with a value
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
            Buffer.BlockCopy(BodyBytes, 0, buffer, offset, BodyLength);
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
                    var offset = Header.BodyOffset;
                    CurrentSpec.ValueIsJson = buffer.IsJson(offset, TotalLength-1);
                    CurrentSpec.Bytes = new byte[TotalLength-offset];
                    Buffer.BlockCopy(buffer, offset, CurrentSpec.Bytes, 0, TotalLength-offset);
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
