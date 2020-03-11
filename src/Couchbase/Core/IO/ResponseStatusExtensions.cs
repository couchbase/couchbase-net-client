using System;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;

namespace Couchbase.Core.IO
{
    internal static class ResponseStatusExtensions
    {
        public static Exception CreateException(this ResponseStatus status,  KeyValueErrorContext ctx)
        {
            switch (status)
            {
                case ResponseStatus.KeyNotFound:
                    return new DocumentNotFoundException {Context = ctx};
                case ResponseStatus.KeyExists:
                    return new DocumentExistsException { Context = ctx };
                case ResponseStatus.ValueTooLarge:
                    return new ValueToolargeException { Context = ctx };
                case ResponseStatus.InvalidArguments:
                    return new InvalidArgumentException { Context = ctx };
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.Busy:
                case ResponseStatus.NotInitialized:
                    return new TemporaryFailureException { Context = ctx };
                case ResponseStatus.OperationTimeout:
                    return new AmbiguousTimeoutException { Context = ctx };
                case ResponseStatus.Locked:
                    return new DocumentLockedException { Context = ctx };
                case ResponseStatus.DocumentMutationLost:
                    return new MutationLostException { Context = ctx };
                case ResponseStatus.DurabilityInvalidLevel:
                    return new DurabilityLevelNotAvailableException { Context = ctx };
                case ResponseStatus.DurabilityImpossible:
                    return new DurabilityImpossibleException { Context = ctx };
                case ResponseStatus.SyncWriteInProgress:
                    return new DurableWriteInProgressException { Context = ctx };
                case ResponseStatus.SyncWriteAmbiguous:
                    return new DurabilityAmbiguousException { Context = ctx };
                case ResponseStatus.Eaccess:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue: //likely remove
                case ResponseStatus.AuthStale:
                    return new AuthenticationFailureException { Context = ctx };
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    return new NotMyVBucketException { Context = ctx };
                case ResponseStatus.SubdocXattrUnknownVattr:
                    return new XattrException { Context = ctx };
                case ResponseStatus.SubdocXattrUnknownMacro:
                case ResponseStatus.SubDocXattrInvalidFlagCombo:
                case ResponseStatus.SubDocXattrInvalidKeyCombo:
                case ResponseStatus.SubdocXattrCantModifyVattr:
                case ResponseStatus.SubdocInvalidXattrOrder:
                    return new XattrException { Context = ctx };
                //sub doc errors
                case ResponseStatus.SubdocMultiPathFailureDeleted:
                case ResponseStatus.SubDocMultiPathFailure:
                case ResponseStatus.SubDocPathNotFound:
                case ResponseStatus.SubDocPathMismatch:
                case ResponseStatus.SubDocPathInvalid:
                    return new PathInvalidException { Context = ctx };
                case ResponseStatus.SubDocPathTooBig:
                    return new PathTooDeepException { Context = ctx };
                case ResponseStatus.SubDocDocTooDeep:
                    return new DocumentTooDeepException { Context = ctx };
                case ResponseStatus.SubDocCannotInsert:
                    return new ValueNotJsonException { Context = ctx };
                case ResponseStatus.SubDocDocNotJson:
                    return new DocumentNotJsonException { Context = ctx };
                case ResponseStatus.SubDocNumRange:
                    return new NumberTooBigException { Context = ctx };
                case ResponseStatus.SubDocDeltaRange:
                    return new DeltaInvalidException { Context = ctx };
                case ResponseStatus.SubDocPathExists:
                    return new PathExistsException { Context = ctx };
                case ResponseStatus.SubDocValueTooDeep:
                    return new DocumentTooDeepException { Context = ctx };
                case ResponseStatus.SubDocInvalidCombo:
                    return new InvalidArgumentException { Context = ctx };
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
                    return new CouchbaseException { Context = ctx };
                case ResponseStatus.UnknownCollection:
                    return new CollectionOutdatedException { Context = ctx };
                default:
                    return new CouchbaseException { Context = ctx };
            }
        }
    }
}
