namespace Couchbase.Stellar.Core.Retry;

public static class StellarRetryStrings
{
    public const string CasMismatch = "CAS_MISMATCH";
    public const string Locked = "LOCKED";
    public const string Unlocked = "NOT_LOCKED";
    public const string DocTooDeep = "DOC_TOO_DEEP";
    public const string DocNotJson = "DOC_NOT_JSON";
    public const string PathMismatch = "PATH_MISMATCH";
    public const string ValueOutOfRange = "VALUE_OUT_OF_RANGE";
    public const string PathValueOutOfRange = "PATH_VALUE_OUT_OF_RANGE";
    public const string ValueTooLarge = "VALUE_TOO_LARGE";

    public const string TypeUrlPreconditionFailure = "type.googleapis.com/google.rpc.PreconditionFailure";
    public const string TypeUrlResourceInfo = "type.googleapis.com/google.rpc.ResourceInfo";
    public const string TypeUrlErrorInfo = "type.googleapis.com/google.rpc.ErrorInfo";
    public const string TypeUrlBadRequest = "type.googleapis.com/google.rpc.BadRequest";

    public const string PreconditionCas = "CAS";
    public const string PreconditionLocked = "LOCKED";
    public const string PreconditionPathMismatch = "PATH_MISMATCH";
    public const string PreconditionDocNotJson = "DOC_NOT_JSON";
    public const string PreconditionDocTooDeep = "DOC_TOO_DEEP";
    public const string PreconditionValueOutOfRange = "VALUE_OUT_OF_RANGE";
    public const string PreconditionPathValueOutOfRange = "PATH_VALUE_OUT_OF_RANGE";
    public const string PreconditionValueTooLarge = "VALUE_TOO_LARGE";

    public const string ResourceTypeDocument = "document";
    public const string ResourceTypeSearchIndex = "searchindex";
    public const string ResourceTypeQueryIndex = "queryindex";
    public const string ResourceTypeAnalyticsIndex = "analyticsindex";
    public const string ResourceTypeBucket = "bucket";
    public const string ResourceTypeScope = "scope";
    public const string ResourceTypeCollection = "collection";
    public const string ResourceTypePath = "path";

    public const string ReasonCasMismatch = "CAS_MISMATCH";
}
