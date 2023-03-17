using System.IO.Pipelines;
using Couchbase.Core.Utils;
using Couchbase.Management.Buckets;

namespace Couchbase.Utils;

internal static class QueryContext
{
    private const string _default = "default:";

    public static string Create(string bucketName, string scopeName)
    {
        return $"default:{bucketName.EscapeIfRequired()}.{scopeName.EscapeIfRequired()}";
    }

    public static string Create(string bucketName)
    {
        return $"default:{bucketName.EscapeIfRequired()}";
    }

    public static string Create()
    {
        return _default;
    }

    public static string CreateOrDefault(string bucketName = null, string scopeName = null)
    {
        if (bucketName == null && scopeName == null)
            return Create();
        if (bucketName != null && scopeName == null)
            return Create(bucketName);

        return Create(bucketName ?? "default", scopeName);
    }
}
