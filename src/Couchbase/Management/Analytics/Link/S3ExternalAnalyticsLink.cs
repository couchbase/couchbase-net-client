#nullable enable
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Couchbase.Management.Analytics.Link
{
    public record S3ExternalAnalyticsLink(
        string Name,
        string Dataverse,
        string AccessKeyId,
        string SecretAccessKey,
        string Region) : AnalyticsLink(Name, Dataverse)
    {
        public override string LinkType => "s3";
        public string? SessionToken { get; init; }
        public string? ServiceEndpoint { get; init; }

        #region BuilderPattern
        // builder pattern boilerplate for users without access to C# 9
        public S3ExternalAnalyticsLink WithSessionToken(string? sessionToken) => this with { SessionToken = sessionToken };
        public S3ExternalAnalyticsLink WithServiceEndpoint(string? serviceEndpoint) => this with { ServiceEndpoint = serviceEndpoint };
        public S3ExternalAnalyticsLink WithSecretAccessKey(string secreteAccessKey) => this with { SecretAccessKey = secreteAccessKey };
        public S3ExternalAnalyticsLink WithRegion(string region) => this with { Region = region };
        #endregion

        public override bool TryValidateForRequest(out List<string> errors)
        {
            base.TryValidateForRequest(out errors);
            RequiredToBeSet(nameof(AccessKeyId), AccessKeyId, errors);
            RequiredToBeSet(nameof(SecretAccessKey), SecretAccessKey, errors);
            RequiredToBeSet(nameof(Region), Region, errors);
            return errors.Count == 0;
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetFormData()
        {
            foreach (var kvp in base.GetFormData())
            {
                yield return kvp;
            }

            yield return new KeyValuePair<string, string>("accessKeyId", AccessKeyId);
            yield return new KeyValuePair<string, string>("secretAccessKey", SecretAccessKey);
            yield return new KeyValuePair<string, string>("region", Region);
            if (!string.IsNullOrEmpty(SessionToken))
            {
                yield return new KeyValuePair<string, string>("sessionToken", SessionToken!);
            }

            if (!string.IsNullOrEmpty(ServiceEndpoint))
            {
                yield return new KeyValuePair<string, string>("serviceEndpoint", ServiceEndpoint!);
            }
        }
    }

    internal record S3ExternalAnalyticsLinkResponse(
        [JsonProperty("name")]
        string Name,

        [JsonProperty("dataverse")]
        string? DataverseFromDataverse,

        [JsonProperty("scope")]
        string? DataverseFromScope,

        [JsonProperty("accessKeyId")]
        string AccessKeyId,

        [JsonProperty("region")]
        string Region
    ) : AnalyticsLinkResponseRecord(Name, DataverseFromDataverse, DataverseFromScope)
    {
        [JsonProperty("serviceEndpoint")]
        public string? ServiceEndpoint { get; init; }

        public S3ExternalAnalyticsLink AsRequest() => new S3ExternalAnalyticsLink(Name, DataverseFromEither, AccessKeyId, string.Empty, Region)
        {
            ServiceEndpoint = ServiceEndpoint
        };
    }
}
