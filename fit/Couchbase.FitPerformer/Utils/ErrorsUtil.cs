using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.Exceptions.View;
using Couchbase.Management.Collections;
using Couchbase.Management.Users;
using Couchbase.Management.Buckets;
using Couchbase.Core.RateLimiting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Exception = Couchbase.Grpc.Protocol.Shared.Exception;
using TimeoutException = Couchbase.Core.Exceptions.TimeoutException;

namespace Couchbase.FitPerformer.Utils
{
    class ErrorsUtil
    {
        public static CouchbaseExceptionType ConvertException(CouchbaseException err)
        {
            // Make sure to return most specific error
            if (err is AmbiguousTimeoutException) return CouchbaseExceptionType.SdkAmbiguousTimeoutException;
            if (err is UnambiguousTimeoutException) return CouchbaseExceptionType.SdkUnambiguousTimeoutException;
            if (err is TimeoutException) return CouchbaseExceptionType.SdkTimeoutException;

            if (err is RequestCanceledException) return CouchbaseExceptionType.SdkRequestCancelledException;
            if (err is InvalidArgumentException) return CouchbaseExceptionType.SdkInvalidArgumentException;

            if (err is ServiceNotAvailableException) return CouchbaseExceptionType.SdkServiceNotAvailableException;
            if (err is InternalServerFailureException) return CouchbaseExceptionType.SdkInternalServerFailureException;
            if (err is AuthenticationFailureException) return CouchbaseExceptionType.SdkAuthenticationFailureException;
            if (err is TemporaryFailureException) return CouchbaseExceptionType.SdkTemporaryFailureException;
            if (err is ParsingFailureException) return CouchbaseExceptionType.SdkParsingFailureException;
            if (err is CasMismatchException) return CouchbaseExceptionType.SdkCasMismatchException;
            if (err is Couchbase.Core.Exceptions.BucketNotFoundException) return CouchbaseExceptionType.SdkBucketNotFoundException;
            if (err is Couchbase.Core.Exceptions.CollectionNotFoundException) return CouchbaseExceptionType.SdkCollectionNotFoundException;
            if (err is Couchbase.Management.Buckets.BucketNotFoundException) return CouchbaseExceptionType.SdkBucketNotFoundException;
            if (err is Couchbase.Management.Collections.CollectionNotFoundException) return CouchbaseExceptionType.SdkCollectionNotFoundException;
            if (err is FeatureNotAvailableException) return CouchbaseExceptionType.SdkFeatureNotAvailableException;
            if (err is ScopeNotFoundException) return CouchbaseExceptionType.SdkScopeNotFoundException;
            if (err is IndexNotFoundException) return CouchbaseExceptionType.SdkIndexNotFoundException;
            if (err is IndexExistsException) return CouchbaseExceptionType.SdkIndexExistsException;
            if (err is EncodingFailureException) return CouchbaseExceptionType.SdkEncodingFailureException;
            if (err is DecodingFailureException) return CouchbaseExceptionType.SdkDecodingFailureException;

            if (err is DocumentNotFoundException) return CouchbaseExceptionType.SdkDocumentNotFoundException;
            if (err is DocumentUnretrievableException) return CouchbaseExceptionType.SdkDocumentUnretrievableException;
            if (err is DocumentLockedException) return CouchbaseExceptionType.SdkDocumentLockedException;
            // dotnet does not have ValueTooLargeException
            if (err is DocumentExistsException) return CouchbaseExceptionType.SdkDocumentExistsException;
            if (err is DurabilityLevelNotAvailableException) return CouchbaseExceptionType.SdkDurabilityLevelNotAvailableException;
            if (err is DurabilityImpossibleException) return CouchbaseExceptionType.SdkDurabilityImpossibleException;
            if (err is DurableWriteInProgressException) return CouchbaseExceptionType.SdkDurableWriteInProgressException;
            if (err is DurableWriteReCommitInProgressException) return CouchbaseExceptionType.SdkDurableWriteRecommitInProgressException;
            if (err is PathNotFoundException) return CouchbaseExceptionType.SdkPathNotFoundException;
            if (err is PathMismatchException) return CouchbaseExceptionType.SdkPathMismatchException;
            if (err is PathInvalidException) return CouchbaseExceptionType.SdkPathInvalidException;
            if (err is PathTooBigException) return CouchbaseExceptionType.SdkPathTooBigException;
            if (err is PathTooDeepException) return CouchbaseExceptionType.SdkPathTooDeepException;
            if (err is ValueTooDeepException) return CouchbaseExceptionType.SdkValueTooDeepException;
            if (err is ValueInvalidException) return CouchbaseExceptionType.SdkValueInvalidException;
            if (err is DocumentTooDeepException) return CouchbaseExceptionType.SdkDocumentTooDeepException;
            if (err is DocumentNotJsonException) return CouchbaseExceptionType.SdkDocumentNotJsonException;
            if (err is NumberTooBigException) return CouchbaseExceptionType.SdkPathTooBigException;
            if (err is DeltaInvalidException) return CouchbaseExceptionType.SdkDeltaInvalidException;
            if (err is PathExistsException) return CouchbaseExceptionType.SdkPathExistsException;
            if (err is XattrUnknownMacroException) return CouchbaseExceptionType.SdkXattrUnknownMacroException;
            if (err is XattrInvalidKeyComboException) return CouchbaseExceptionType.SdkXattrInvalidKeyComboException;
            if (err is XattrUnknownVirtualAttributeException) return CouchbaseExceptionType.SdkXattrUnknownVirtualAttributeException;
            if (err is XattrCannotModifyVirtualAttributeException) return CouchbaseExceptionType.SdkXattrCannotModifyVirtualAttributeException;

            if (err is PlanningFailureException) return CouchbaseExceptionType.SdkPlanningFailureException;
            if (err is IndexFailureException) return CouchbaseExceptionType.SdkIndexFailureException;
            if (err is PreparedStatementException) return CouchbaseExceptionType.SdkPreparedStatementFailureException;

            if (err is CompilationFailureException) return CouchbaseExceptionType.SdkCompilationFailureException;
            if (err is JobQueueFullException) return CouchbaseExceptionType.SdkJobQueueFullException;
            if (err is DatasetNotFoundException) return CouchbaseExceptionType.SdkDatasetNotFoundException;
            if (err is DataverseNotFoundException) return CouchbaseExceptionType.SdkDataverseNotFoundException;
            if (err is DatasetExistsException) return CouchbaseExceptionType.SdkDatasetExistsException;
            if (err is DataverseExistsException) return CouchbaseExceptionType.SdkDataverseExistsException;
            if (err is LinkNotFoundException) return CouchbaseExceptionType.SdkLinkNotFoundException;

            if (err is ViewNotFoundException) return CouchbaseExceptionType.SdkViewNotFoundException;
            if (err is DesignDocumentNotFoundException) return CouchbaseExceptionType.SdkDesignDocumentNotFoundException;

            if (err is CollectionExistsException) return CouchbaseExceptionType.SdkCollectionExistsException;
            if (err is ScopeExistsException) return CouchbaseExceptionType.SdkScopeExistsException;
            if (err is UserNotFoundException) return CouchbaseExceptionType.SdkUserNotFoundException;
            // GroupNotFoundException is private
            if (err is BucketExistsException) return CouchbaseExceptionType.SdkBucketExistsException;
            // UserExistsException is private
            // Does not have BucketNotFlushableException.

            if (err is RateLimitedException) return CouchbaseExceptionType.SdkRateLimitedException;
            if (err is QuotaLimitedException) return CouchbaseExceptionType.SdkQuotaLimitedException;
            if (err is DmlFailureException) return CouchbaseExceptionType.SdkDmlFailureException;
            // Does not have XattrNoAccessException

            if (err is DocumentNotLockedException) return CouchbaseExceptionType.SdkDocumentNotLockedException;

            if (err is UnsupportedException) return CouchbaseExceptionType.SdkUnsupportedOperationException;

            if (err is BucketIsNotFlushableException) return CouchbaseExceptionType.SdkBucketNotFlushableException;

            return CouchbaseExceptionType.SdkCouchbaseException;
        }

        public static Exception ConvertException(System.Exception raw)
        {
            var ret = new Exception();

            if (raw is CouchbaseException cbe)
            {
                var type = ConvertException(cbe);

                var sdk = new CouchbaseExceptionEx();
                sdk.Name = raw.GetType().Name;
                sdk.Type = type;
                sdk.Serialized = JsonConvert.SerializeObject(cbe, new StringEnumConverter());
                if (raw.InnerException != null)
                {
                    sdk.Cause = ConvertException(cbe.InnerException);
                }

                ret.Couchbase = sdk;
            }

            return ret;
        }
    }
}