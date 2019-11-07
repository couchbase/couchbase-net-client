using System;
using System.Collections.Generic;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularBase<T> : OperationBase<T>
    {
        protected ITypeSerializerProvider Builder;
        protected OperationSpec CurrentSpec;

        public string Path { get; protected set; }

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

        public override void WriteFramingExtras(OperationBuilder builder)
        {
        }

        public override T GetValue()
        {
            var result = default(T);
            if (Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span;
                    ReadExtras(buffer);
                    var offset = Header.BodyOffset;
                    CurrentSpec.ValueIsJson = buffer.Slice(offset, TotalLength).IsJson();
                    var payload = new byte[TotalLength-offset];
                    buffer.Slice(offset, CurrentSpec.Bytes.Length).CopyTo(payload);
                    CurrentSpec.Bytes = payload;
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
