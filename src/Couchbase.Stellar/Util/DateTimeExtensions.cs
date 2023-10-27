namespace Couchbase.Stellar.Util;

internal static class DateTimeExtensions
{
    public static DateTime FromNow(this TimeSpan expiry) => DateTime.UtcNow.Add(expiry);
    public static DateTime? FromNow(this TimeSpan? expiry) => expiry?.FromNow();
}
