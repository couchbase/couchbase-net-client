using System;
using System.Text;

namespace Couchbase.Utils;

internal static class CouchbaseStrings
{
    public static string MinimumPattern { get; } = Encoding.UTF8.GetString(new byte[] { 0x00 });
    public static string MaximumPattern { get; } = Encoding.UTF8.GetString(new byte[] { 0xF4, 0x8F, 0xBF, 0xBF });
    public static ReadOnlySpan<byte> TrueBytes => new byte[] { 0x74, 0x72, 0x75, 0x65 };
    public static ReadOnlySpan<byte> FalseBytes => new byte[] { 0x66, 0x61, 0x6C, 0x73, 0x65 };
}
