using System;
using System.IO;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.RateLimiting;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using CollectionNotFoundException = Couchbase.Core.Exceptions.CollectionNotFoundException;

namespace Couchbase.Core.IO
{
    internal static class ResponseStatusExtensions
    {
        public static Exception CreateException(this ResponseStatus status,  KeyValueErrorContext ctx, IOperation op)
        {
            switch (status)
            {
                case ResponseStatus.KeyNotFound:
                    return new DocumentNotFoundException {Context = ctx};
                case ResponseStatus.KeyExists:
                    if (ctx.OpCode != OpCode.Add && ctx.OpCode != OpCode.SubMultiMutation)
                    {
                        return new CasMismatchException { Context = ctx };
                    }
                    return new DocumentExistsException { Context = ctx };
                case ResponseStatus.ValueTooLarge:
                    return new ValueToolargeException { Context = ctx };
                case ResponseStatus.InvalidArguments:
                    return new InvalidArgumentException { Context = ctx };
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.Busy:
                case ResponseStatus.NotInitialized:
                    return new TemporaryFailureException(status.ToString()) { Context = ctx };
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
                case ResponseStatus.SyncWriteReCommitInProgress:
                    return new DurableWriteReCommitInProgressException { Context = ctx };
                case ResponseStatus.SyncWriteAmbiguous:
                    return new DurabilityAmbiguousException { Context = ctx };
                case ResponseStatus.Eaccess:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue: //likely remove
                case ResponseStatus.AuthStale:
                    return new AuthenticationFailureException("Either the bucket is not present, " +
                        "the user does not have the right privileges to access it, " +
                        "or the bucket is hibernated: " + status.ToString())
                    { Context = ctx };
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
                case ResponseStatus.SubDocMultiPathFailure:
                    return SubDocPathException(ctx, op);
                case ResponseStatus.SubdocMultiPathFailureDeleted:
                case ResponseStatus.SubDocPathNotFound:
                case ResponseStatus.SubDocPathMismatch:
                case ResponseStatus.SubDocPathInvalid:
                    // Shouldn't be possible at this point, since these are body-level codes, not top-level
                    // but just in case
                    return new SubdocExceptionException() { Context = ctx, SubDocumentErrorIndex = null, SubDocumentStatus = status };
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
                    return new CollectionNotFoundException { Context = ctx };
                case ResponseStatus.UnknownScope:
                    return new ScopeNotFoundException { Context = ctx};
                case ResponseStatus.TransportFailure:
                    return new RequestCanceledException{ Context = ctx };
                case ResponseStatus.NoCollectionsManifest:
                    return new UnsupportedException("Non-default Scopes and Collections not supported on this server version.") { Context = ctx };
                    //Rate limiting Errors Below
                case ResponseStatus.RateLimitedNetworkEgress:
                    return new RateLimitedException(RateLimitedReason.NetworkEgressRateLimitReached, ctx);
                case ResponseStatus.RateLimitedNetworkIngress:
                    return new RateLimitedException(RateLimitedReason.NetworkIngressRateLimitReached, ctx);
                case ResponseStatus.RateLimitedMaxConnections:
                    return new RateLimitedException(RateLimitedReason.MaximumConnectionsReached, ctx);
                case ResponseStatus.RateLimitedMaxCommands:
                    return new RateLimitedException(RateLimitedReason.RequestRateLimitReached, ctx);
                case ResponseStatus.ScopeSizeLimitExceeded:
                    return new QuotaLimitedException(QuotaLimitedReason.ScopeSizeLimitExceeded, ctx);
                default:
                    return new CouchbaseException { Context = ctx };
            }
        }

        private static SubdocExceptionException SubDocPathException(KeyValueErrorContext ctx, IOperation op)
        {
            var subdocStatusBody = op.ExtractBody();
            byte index = subdocStatusBody.Memory.Span[0];
            var subdocErrorStatus = (ResponseStatus)ByteConverter.ToUInt16(subdocStatusBody.Memory.Span.Slice(1));
            SubdocExceptionException ex = subdocErrorStatus switch
            {
                ResponseStatus.SubDocPathExists => new PathExistsException(),
                ResponseStatus.SubDocPathInvalid => new PathInvalidException(),
                ResponseStatus.SubDocPathMismatch => new PathMismatchException(),
                ResponseStatus.SubDocPathNotFound => new PathNotFoundException(),
                ResponseStatus.SubDocPathTooBig => new PathTooBigException(),
                _ => new SubdocExceptionException() { Context = ctx, SubDocumentStatus = subdocErrorStatus, SubDocumentErrorIndex = index }
            };

            ex.Context = ctx;
            ex.SubDocumentStatus = subdocErrorStatus;
            ex.SubDocumentErrorIndex = index;
            return ex;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
