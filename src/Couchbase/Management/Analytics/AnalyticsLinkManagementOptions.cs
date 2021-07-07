#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Management.Analytics
{
    public abstract record AnalyticsLinkManagementOptions
    {
        public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    }


    public record CreateAnalyticsLinkOptions : AnalyticsLinkManagementOptions;
    public record ReplaceAnalyticsLinkOptions : AnalyticsLinkManagementOptions;
    public record DropAnalyticsLinkOptions : AnalyticsLinkManagementOptions;
    public record GetAnalyticsLinksOptions : AnalyticsLinkManagementOptions
    {
        public string? DataverseName { get; init; }
        public string? Name { get; init; }
        public string? LinkType { get; init; }

        public GetAnalyticsLinksOptions WithDataverseName(string dataverseName) => this with { DataverseName = dataverseName };
        public GetAnalyticsLinksOptions WithName(string linkName) => this with { Name = linkName };
        public GetAnalyticsLinksOptions WithLinkType(string linkType) => this with { LinkType = linkType };
    }

    public static class AnalyticsOptionsExtensions
    {
        public static T WithCancellationToken<T>(this T opts, CancellationToken token)
            where T : AnalyticsLinkManagementOptions
        {
            return opts with { CancellationToken = token };
        }
    }
}
