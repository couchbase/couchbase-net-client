#nullable enable
using Couchbase.Core.Compatibility;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Couchbase.Management.Analytics.Link
{
    [InterfaceStability(Level.Volatile)]
    public record AzureBlobExternalAnalyticsLink(
        string Name,
        string Dataverse
        ) : AnalyticsLink(Name, Dataverse)
    {
        public override string LinkType => "azureblob";
        
        public string? ConnectionString { get; init; }
        public string? AccountName { get; init; }
        public string? AccountKey { get; init; }
        public string? SharedAccessSignature { get; init; }
        public string? BlobEndpoint { get; init; }
        public string? EndpointSuffix { get; init; }

        #region BuilderPattern
        // builder pattern boilerplate for users without access to C# 9
        public AzureBlobExternalAnalyticsLink WithConnectionString(string? connectionString) => this with { ConnectionString = connectionString };
        public AzureBlobExternalAnalyticsLink WithAccountName(string? accountName) => this with { AccountName = accountName };
        public AzureBlobExternalAnalyticsLink WithAccountKey(string? accountKey) => this with { AccountKey = accountKey };
        public AzureBlobExternalAnalyticsLink WithSharedAccessSignature(string? sharedAccessSignature) => this with { SharedAccessSignature = sharedAccessSignature };
        public AzureBlobExternalAnalyticsLink WithBlobEndpoint(string? blobEndpoint) => this with { BlobEndpoint = blobEndpoint };
        public AzureBlobExternalAnalyticsLink WithEndpointSuffix(string? endpointSuffix) => this with { EndpointSuffix = endpointSuffix };
        #endregion

        public override bool TryValidateForRequest(out List<string> errors)
        {
            base.TryValidateForRequest(out errors);
            bool oneValidCombo = false;
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                oneValidCombo = true;
            }

            if (!string.IsNullOrEmpty(AccountName))
            {
                oneValidCombo = oneValidCombo || (!string.IsNullOrEmpty(AccountKey)) || (!string.IsNullOrEmpty(SharedAccessSignature));
            }

            if (!oneValidCombo)
            {
                errors.Add($"Either {nameof(ConnectionString)} or {nameof(AccountName)} and one of {nameof(AccountKey)}/{nameof(SharedAccessSignature)} must be provided.");
            }

            return errors.Count == 0;
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetFormData()
        {
            foreach (var kvp in base.GetFormData())
            {
                yield return kvp;
            }

            if (ConnectionString != null)
            {
                yield return new KeyValuePair<string, string>("connectionString", ConnectionString);
            }

            if (AccountName != null)
            {
                yield return new KeyValuePair<string, string>("accountName", AccountName);
            }

            if (AccountKey != null)
            {
                yield return new KeyValuePair<string, string>("accountKey", AccountKey);
            }

            if (SharedAccessSignature != null)
            {
                yield return new KeyValuePair<string, string>("sharedAccessSignature", SharedAccessSignature);
            }

            if (BlobEndpoint != null)
            {
                yield return new KeyValuePair<string, string>("blobEndpoint", BlobEndpoint);
            }

            if (EndpointSuffix != null)
            {
                yield return new KeyValuePair<string, string>("endpointSuffix", EndpointSuffix);
            }
        }
    }

    internal record AzureBlobExternalAnalyticsLinkResponse(
        [JsonProperty("name")]
        string Name,

        [JsonProperty("dataverse")]
        string? DataverseFromDataverse,

        [JsonProperty("scope")]
        string? DataverseFromScope) : AnalyticsLinkResponseRecord(Name, DataverseFromDataverse, DataverseFromScope)
    {
        [JsonProperty("accountName")]
        public string? AccountName { get; init; }

        [JsonProperty("blobEndpoint")]
        public string? BlobEndpoint { get; init; }

        [JsonProperty("endpointSuffix")]
        public string? EndpointSuffix { get; init; }

        public AzureBlobExternalAnalyticsLink AsRequest() => new AzureBlobExternalAnalyticsLink(Name, DataverseFromEither) with
        {
            AccountName = AccountName,
            BlobEndpoint = BlobEndpoint,
            EndpointSuffix = EndpointSuffix,
        };
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
