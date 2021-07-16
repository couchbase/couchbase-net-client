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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
