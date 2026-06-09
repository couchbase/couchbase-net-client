#nullable enable

using System;
using Couchbase.Grpc.Protocol.Transactions;
using System.Linq;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Error.Attempts;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Grpc.Protocol.Shared;
using Newtonsoft.Json.Linq;
using Exception = System.Exception;


namespace Couchbase.FitPerformer.Utils.Results
{
    internal static class TxnResultsUtil
    {
        public static Couchbase.Grpc.Protocol.Transactions.TransactionResult CreateResult(
            Couchbase.Client.Transactions.TransactionResult? result, Exception? exception, int? cleanupRequests = null)
        {
            var response = new Couchbase.Grpc.Protocol.Transactions.TransactionResult();

            if (result == null &&
                exception is TransactionFailedException transactionFailedException)
            {
                result = transactionFailedException.Result;
            }

            if (result != null)
            {
                response.TransactionId = result.TransactionId;
                response.UnstagingComplete = result.UnstagingComplete;
                if (cleanupRequests.HasValue)
                {
                    response.CleanupRequestsValid = true;
                    response.CleanupRequestsPending = cleanupRequests.Value;
                }
                else
                {
                    response.CleanupRequestsValid = false;
                }

                response.Log.AddRange(result.Logs);
            }

            if (exception != null)
            {
                response.Exception = ConvertTransactionFailed(exception);
                var cause = exception.InnerException ?? exception;
                response.ExceptionCause = MapCause(cause);
                Serilog.Log.Debug("UNKNOWN due to {Cause}", cause);
            }
            else
            {
                response.Exception = TransactionException.NoExceptionThrown;
            }

            return response;
        }

        internal static TransactionException ConvertTransactionFailed(Exception ex) => ex switch
        {
            TransactionExpiredException => TransactionException.ExceptionExpired,
            TransactionCommitAmbiguousException => TransactionException.ExceptionCommitAmbiguous,
            TransactionFailedException => TransactionException.ExceptionFailed,
            _ => TransactionException.ExceptionUnknown,
        };

        public static ExternalException MapCause(Exception exception) => exception switch
        {
            null => ExternalException.NotSet,
            // the following _only_ matches with System.Exception, not derived classes.  The
            // point here is the default when nothing was set is Exception.  So that (or null)
            // is NotSet.  Ideally, we'd make this nullable, and not have an Exception at all.
            // That is part of a future surgerical procedure...
            Exception e when e.GetType() == typeof(Exception) => ExternalException.NotSet,
            ActiveTransactionRecordsFullException _ =>
                ExternalException.ActiveTransactionRecordFull,
            ActiveTransactionRecordNotFoundException _ => ExternalException
                .ActiveTransactionRecordNotFound,
            ActiveTransactionRecordEntryNotFoundException _ => ExternalException
                .ActiveTransactionRecordEntryNotFound,
            FeatureNotAvailableException _ => ExternalException.FeatureNotAvailableException,
            NotSupportedException _ => ExternalException.FeatureNotAvailableException,
            DocumentAlreadyInTransactionException _ => ExternalException
                .DocumentAlreadyInTransaction,
            DocumentExistsException _ => ExternalException.DocumentExistsException,
            DocumentNotFoundException _ => ExternalException.DocumentNotFoundException,
            PreviousOperationFailedException _ => ExternalException.PreviousOperationFailed,
            ForwardCompatibilityFailureException _ => ExternalException.ForwardCompatibilityFailure,
            ForwardCompatibilityFailureRequiresRetryException _ => ExternalException
                .ForwardCompatibilityFailure,
            ParsingFailureException _ => ExternalException.ParsingFailure,
            TransactionOperationFailedException tof => MapCause(tof.Cause),
            InvalidOperationException _ => ExternalException.IllegalStateException,
            ServiceNotAvailableException _ => ExternalException.ServiceNotAvailableException,
            UnambiguousTimeoutException _ => ExternalException.UnambiguousTimeoutException,
            AmbiguousTimeoutException _ => ExternalException.AmbiguousTimeoutException,
            AuthenticationFailureException _ => ExternalException.AuthenticationFailureException,
            TransactionAlreadyCommittedException _ => ExternalException.TransactionAlreadyCommitted,
            TransactionAlreadyAbortedException _ => ExternalException.TransactionAlreadyAborted,
            CommitNotPermittedException _ => ExternalException.CommitNotPermitted,
            RollbackNotPermittedException _ => ExternalException.RollbackNotPermitted,
            ConcurrentOperationsDetectedOnSameDocumentException _ => ExternalException
                .ConcurrentOperationsDetectedOnSameDocument,
            DocumentUnretrievableException _ => ExternalException.DocumentUnretrievableException,
            CouchbaseException _ => ExternalException.CouchbaseException,
            _ => ExternalException.Unknown
        };

        public static Grpc.Protocol.Transactions.ErrorClass MapErrorClass(
            Couchbase.Client.Transactions.Error.ErrorClass errorClass)
        {
            switch (errorClass)
            {
                case Couchbase.Client.Transactions.Error.ErrorClass.FailDocAlreadyExists:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailDocAlreadyExists;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailHard:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailHard;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailPathAlreadyExists:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailPathAlreadyExists;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailOther:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailOther;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailTransient:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailTransient;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailDocNotFound:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailDocNotFound;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailPathNotFound:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailPathNotFound;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailCasMismatch:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailCasMismatch;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailAmbiguous:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailAmbiguous;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailAtrFull:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailAtrFull;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailWriteWriteConflict:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailWriteWriteConflict;
                case Couchbase.Client.Transactions.Error.ErrorClass.FailExpiry:
                    return Grpc.Protocol.Transactions.ErrorClass.EcFailExpiry;
                default:
                    throw new InternalPerformerException(
                        $"Given error class '{errorClass}' is not supported",
                        new NotSupportedException());
            }
        }

        public static TransactionException MapToRaise(
            TransactionOperationFailedException.FinalError finalError)
        {
            switch (finalError)
            {
                case TransactionOperationFailedException.FinalError.TransactionFailed:
                    return TransactionException.ExceptionFailed;
                case TransactionOperationFailedException.FinalError.TransactionExpired:
                    return TransactionException.ExceptionExpired;
                case TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous:
                    return TransactionException.ExceptionCommitAmbiguous;
                case TransactionOperationFailedException.FinalError.TransactionFailedPostCommit:
                    return TransactionException.ExceptionFailedPostCommit;
                default:
                    throw new NotSupportedException(
                        $"Given final error to raise '{finalError}' is not supported");
            }
        }

        private static AttemptStates MapState(Couchbase.Client.Transactions.Support.AttemptStates state)
        {
            switch (state)
            {
                case Couchbase.Client.Transactions.Support.AttemptStates.ABORTED:
                    return AttemptStates.Aborted;
                case Couchbase.Client.Transactions.Support.AttemptStates.COMMITTED:
                    return AttemptStates.Committed;
                case Couchbase.Client.Transactions.Support.AttemptStates.NOTHING_WRITTEN:
                    return AttemptStates.NothingWritten;
                case Couchbase.Client.Transactions.Support.AttemptStates.COMPLETED:
                    return AttemptStates.Completed;
                case Couchbase.Client.Transactions.Support.AttemptStates.PENDING:
                    return AttemptStates.Pending;
                case Couchbase.Client.Transactions.Support.AttemptStates.ROLLED_BACK:
                    return AttemptStates.RolledBack;
                case Couchbase.Client.Transactions.Support.AttemptStates.UNKNOWN:
                    return AttemptStates.Unknown;
                default:
                    throw new ArgumentException($"Unknown state: {state}");
            }
        }

        internal static TransactionCleanupAttempt MapCleanupAttempt(
            Couchbase.Client.Transactions.Cleanup.TransactionCleanupAttempt attempt) => new TransactionCleanupAttempt()
        {
            Atr = new DocId()
            {
                BucketName = attempt.AtrBucketName,
                CollectionName = attempt.AtrCollectionName,
                ScopeName = attempt.AtrScopeName,
                DocId_ = attempt.AtrId
            },
            AttemptId = attempt.AttemptId,
            Success = attempt.Success
        };

        // txns only use objects and binary, so we will skip being thorough for now.
        public static void ProcessExpectedContent(ContentAsPerformerValidation? contentAs, dynamic? result)
        {
            if (contentAs == null) return;
            // first, existence.
            if (contentAs.ExpectSuccess && result == null)
            {
                throw new TestFailureException("Expected result to be non-null");
            }

            if (!contentAs.ExpectSuccess && result != null)
            {
                throw new TestFailureException("Expected result to be null");
            }
            // we now parse the ContentAs, and compare, raising an exception if they don't match
            if (contentAs.HasExpectedContentBytes)
            {
                byte[]? expected = contentAs.ExpectedContentBytes.ToByteArray();
                byte[]? actual = result?.ContentAs<byte[]>();
                if (expected != null && actual != null)
                {
                    if (expected.SequenceEqual(actual)) return;
                }

                Serilog.Log.Warning("Expected content '{Expected}' didn't match actual '{Actual}' ",
                    System.Text.Encoding.UTF8.GetString(expected ?? Array.Empty<byte>()),
                    System.Text.Encoding.UTF8.GetString(actual ?? Array.Empty<byte>()));
                throw new TestFailureException("Content didn't match expectations");
            }

            if (contentAs.ContentAs.HasAsJsonObject)
            {
                var expected = contentAs.ContentAs.AsJsonObject;
                var actual = result?.ContentAs<JObject>();
                if (JToken.DeepEquals(expected, actual)) return;
                Serilog.Log.Warning("Expected content {Expected} didn't match actual content {Actual}",
                    expected.ToString(), (actual ?? "<null>").ToString());
            }
        }
    }
}
