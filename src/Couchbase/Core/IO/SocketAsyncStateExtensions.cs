using System;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
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
                    return new DocumentNotFoundException
                    {
                        Context = new KeyValueErrorContext
                        {
                            
                        }
                    };
                case ResponseStatus.KeyExists:
                    return new DocumentExistsException();
                case ResponseStatus.ValueTooLarge:
                    return new ValueToolargeException();
                case ResponseStatus.InvalidArguments:
                    return new InvalidArgumentException();
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.Busy:
                case ResponseStatus.NotInitialized:
                    return new TemporaryFailureException();
                case ResponseStatus.OperationTimeout:
                    return new RequestTimeoutException();
                case ResponseStatus.Locked:
                    return new DocumentLockedException();
                case ResponseStatus.DocumentMutationLost:
                    return new MutationLostException();
                case ResponseStatus.DurabilityInvalidLevel:
                    return new DurabilityLevelNotAvailableException();
                case ResponseStatus.DurabilityImpossible:
                    return new DurabilityImpossibleException();
                case ResponseStatus.SyncWriteInProgress:
                    return new DurableWriteInProgressException();
                case ResponseStatus.SyncWriteAmbiguous:
                    return new DurabilityAmbiguousException();
                case ResponseStatus.Eaccess:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue: //likely remove
                case ResponseStatus.AuthStale:
                    return new AuthenticationException();
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    return new NotMyVBucketException();
                case ResponseStatus.SubdocXattrUnknownVattr:
                    return new XattrException();
                case ResponseStatus.SubdocXattrUnknownMacro:
                case ResponseStatus.SubDocMultiPathFailure:
                case ResponseStatus.SubDocXattrInvalidFlagCombo:
                case ResponseStatus.SubDocXattrInvalidKeyCombo:
                case ResponseStatus.SubdocXattrCantModifyVattr:
                case ResponseStatus.SubdocMultiPathFailureDeleted:
                case ResponseStatus.SubdocInvalidXattrOrder:
                    return new XattrException();
                //sub doc errors
                case ResponseStatus.SubDocPathNotFound:
                case ResponseStatus.SubDocPathMismatch:
                case ResponseStatus.SubDocPathInvalid:
                    return new PathInvalidException();
                case ResponseStatus.SubDocPathTooBig:
                    return new PathTooDeepException();
                case ResponseStatus.SubDocDocTooDeep:
                    return new DocumentTooDeepException();
                case ResponseStatus.SubDocCannotInsert:
                    return new CannotInsertValueException();
                case ResponseStatus.SubDocDocNotJson:
                    return new DocumentNotJsonException();
                case ResponseStatus.SubDocNumRange:
                    return new NumberTooBigException();
                case ResponseStatus.SubDocDeltaRange:
                    return new DeltaRangeException();
                case ResponseStatus.SubDocPathExists:
                    return new PathExistsException();
                case ResponseStatus.SubDocValueTooDeep:
                    return new DocumentTooDeepException();
                case ResponseStatus.SubDocInvalidCombo:
                    return new InvalidArgumentException();
                case ResponseStatus.DocumentMutationDetected: //maps to nothing
                case ResponseStatus.NoReplicasFound: //maps to nothing
                case ResponseStatus.InvalidRange: //maps to nothing
                case ResponseStatus.ItemNotStored: //maps to nothing
                case ResponseStatus.IncrDecrOnNonNumericValue: //maps to nothing
                case ResponseStatus.Rollback: //maps to nothing
                case ResponseStatus.InternalError: //maps to nothing
                case ResponseStatus.UnknownCommand: //maps to nothing
                case ResponseStatus.BucketNotConnected: //maps to nothing
                case ResponseStatus.NotSupported: //maps to nothing
                    return new CouchbaseException();
                default:
                    return new ArgumentOutOfRangeException();
            }
        }
    }
}
