using System.Text;

namespace Couchbase.Utils;

internal record CouchbaseStrings
{
    public static string MinimumPattern { get; } = Encoding.UTF8.GetString(new byte[] { 0x00 });
    public static string MaximumPattern { get; } = Encoding.UTF8.GetString(new byte[] { 0xF4, 0x8F, 0xBF, 0xBF });
}
