#nullable enable

namespace Couchbase.Management.Analytics.Link
{
    /// <summary>
    /// A placeholder record used for deserializing JSON representations of <see cref="AnalyticsLink"/>
    /// with redacted information.
    /// </summary>
    internal abstract record AnalyticsLinkResponseRecord(
        string Name,
        string? DataverseFromDataverse,
        string? DataverseFromScope)
    {
        public string DataverseFromEither => DataverseFromDataverse ?? DataverseFromScope ?? string.Empty;
    }
}
