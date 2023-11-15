using Couchbase.Core.Exceptions;

namespace Couchbase.Stellar.Util;

public class UnsupportedInProtostellarException : FeatureNotAvailableException
{
    public UnsupportedInProtostellarException(string? feature) : base($"The feature {feature} is not supported when using Protostellar.")
    {
    }
}
