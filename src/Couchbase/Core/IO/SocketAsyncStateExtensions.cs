using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.KeyValue;

namespace Couchbase.Core.IO
{
    public static class SocketAsyncStateExtensions
    {
        public static Exception ThrowException(this SocketAsyncState state, ErrorCode errorCode)
        {
            var statusName = Enum.GetName(typeof(ResponseStatus), state.Status);
            switch (state.Status)
            {
                case ResponseStatus.KeyNotFound:
                    return new KeyNotFoundException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.KeyExists:
                    return new KeyExistsException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.ValueTooLarge:
                    return new ValueTooLargeException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.InvalidArguments:
                    return new InvalidArgumentException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.Busy:
                    return new TempFailException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.OperationTimeout:
                    return new TimeoutException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.Locked:
                    return new KeyLockedException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.DocumentMutationLost:
                case ResponseStatus.DocumentMutationDetected:
                case ResponseStatus.NoReplicasFound:
                case ResponseStatus.DurabilityInvalidLevel:
                case ResponseStatus.DurabilityImpossible:
                case ResponseStatus.SyncWriteInProgress:
                case ResponseStatus.SyncWriteAmbiguous:
                    return new DurabilityException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.Eaccess:
                case ResponseStatus.AuthenticationError:
                    return new AuthenticationException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                //internal errors handled by the app?
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    return new NotMyVBucketException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.UnknownError:
                    return new UnknownErrorException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.Rollback:
                case ResponseStatus.AuthenticationContinue:
                case ResponseStatus.AuthStale:
                case ResponseStatus.InternalError:
                case ResponseStatus.UnknownCommand:
                case ResponseStatus.BucketNotConnected:
                case ResponseStatus.NotInitialized:
                case ResponseStatus.NotSupported:
                case ResponseStatus.SubdocXattrUnknownVattr:
                case ResponseStatus.SubDocMultiPathFailure:
                case ResponseStatus.SubDocXattrInvalidFlagCombo:
                case ResponseStatus.SubDocXattrInvalidKeyCombo:
                case ResponseStatus.SubdocXattrCantModifyVattr:
                case ResponseStatus.SubdocMultiPathFailureDeleted:
                case ResponseStatus.SubdocInvalidXattrOrder:
                    return new InternalErrorException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.InvalidRange:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                    return new KeyValueException //hmm?
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    };
                //sub doc errors
                case ResponseStatus.SubDocPathNotFound:
                    return new PathNotFoundException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.SubDocPathMismatch:
                    return new PathMismatchException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.SubDocPathInvalid:
                    return new PathInvalidException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.SubDocPathTooBig:
                    return new PathTooBigException(statusName, new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    });
                case ResponseStatus.SubDocDocTooDeep:
                case ResponseStatus.SubDocCannotInsert:
                case ResponseStatus.SubDocDocNotJson:
                case ResponseStatus.SubDocNumRange:
                case ResponseStatus.SubDocDeltaRange:
                case ResponseStatus.SubDocPathExists:
                case ResponseStatus.SubDocValueTooDeep:
                case ResponseStatus.SubDocInvalidCombo:
                case ResponseStatus.SubdocXattrUnknownMacro:
                    return new KeyValueException
                    {
                        Status = state.Status,
                        ErrorCode = errorCode
                    };
                //remove these ones
                case ResponseStatus.Failure:
                case ResponseStatus.ClientFailure:
                    break;
                case ResponseStatus.NodeUnavailable:
                    break;
                case ResponseStatus.TransportFailure:
                    return state.Exception;
                default:
                    return new ArgumentOutOfRangeException();
            }

            return new Exception("oh me oh mai...");
        }
    }
}
