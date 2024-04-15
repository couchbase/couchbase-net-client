using System;
using System.Text;

namespace Couchbase.Utils;

internal static class CouchbaseStrings
{
    public const string MinimumPattern = "\0";
    public const string MaximumPattern = "\U0010ffff";
    public static ReadOnlySpan<byte> TrueBytes => "true"u8;
    public static ReadOnlySpan<byte> FalseBytes => "false"u8;
}
