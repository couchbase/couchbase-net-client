#if NETCOREAPP3_1_OR_GREATER
using Couchbase.Core.Exceptions;

namespace Couchbase.Stellar.Util;

#nullable enable

public class UnsupportedInProtostellarException : FeatureNotAvailableException
{
    public UnsupportedInProtostellarException(string? feature) : base($"The feature {feature} is not supported when using Protostellar.")
    {
    }
}
#endif
