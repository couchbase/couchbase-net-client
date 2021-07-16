#nullable enable
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Couchbase.Management.Analytics.Link
{
    public record CouchbaseRemoteAnalyticsLink(
        string Name,
        string Dataverse,
        string Hostname,
        CouchbaseRemoteAnalyticsLink.EncryptionSettings? Encryption = null) : AnalyticsLink(Name, Dataverse)
    {
        #region BuilderPattern
        // builder pattern boilerplate for users without access to C# 9
        public CouchbaseRemoteAnalyticsLink WithUsername(string username) => this with { Username = username };
        public CouchbaseRemoteAnalyticsLink WithPassword(string password) => this with { Password = password };
        #endregion

        public static CouchbaseRemoteAnalyticsLink WithFullEncryption(
            string name,
            string dataverse,
            string hostname,
            string certificate,
            string clientCertificate,
            string clientKey) => new(name, dataverse, hostname, new(EncryptionLevel.Full)
            {
                Certificate = certificate,
                ClientCertificate = clientCertificate,
                ClientKey = clientKey
            });

        public override string LinkType => "couchbase";
        public enum EncryptionLevel
        {
            None,
            Half,
            Full
        }

        public record EncryptionSettings(EncryptionLevel EncryptionLevel)
        {
            public string? Certificate { get; init; }
            public string? ClientCertificate { get; init; }
            public string? ClientKey { get; init; }
        }

        public string? Username { get; init; }
        public string? Password { get; init; }

        public override bool TryValidateForRequest(out List<string> errors)
        {
            base.TryValidateForRequest(out errors);
            bool userAndPass = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
            if (Encryption?.EncryptionLevel == EncryptionLevel.Full)
            {
                RequiredToBeSet(nameof(EncryptionSettings.Certificate), Encryption?.Certificate, errors);
                bool clientCertAndKey = !string.IsNullOrWhiteSpace(Encryption?.ClientCertificate) && !string.IsNullOrWhiteSpace(Encryption?.ClientKey);
                if (!(userAndPass || clientCertAndKey))
                {
                    errors.Add($"EncryptionLevel '{Encryption}' requires both of ({nameof(Username)} and {nameof(Password)}) or ({nameof(EncryptionSettings.ClientCertificate)} and {nameof(EncryptionSettings.ClientKey)}) to be set.");
                }
            }
            else if (!userAndPass)
            {
                errors.Add($"{nameof(Username)} and {nameof(Password)} must be set.");
            }

            return errors.Count == 0;
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetFormData()
        {
            foreach (var kvp in base.GetFormData())
            {
                yield return kvp;
            }

            yield return new KeyValuePair<string, string>("hostname", Hostname);
            yield return new KeyValuePair<string, string>("encryption", Encryption?.EncryptionLevel switch {
                null => "none",
                EncryptionLevel.None => "none",
                EncryptionLevel.Half => "half",
                EncryptionLevel.Full => "full",
                _ => Encryption.ToString().ToLowerInvariant()
            });

            if (Username != null)
            {
                yield return new KeyValuePair<string, string>("username", Username);
            }

            if (Password != null)
            {
                yield return new KeyValuePair<string, string>("password", Password);
            }

            if (Encryption?.EncryptionLevel >= EncryptionLevel.Full)
            {
                _ = Encryption?.Certificate ?? throw new ArgumentNullException(nameof(EncryptionSettings.Certificate));
                yield return new KeyValuePair<string, string>("certificate", Encryption!.Certificate!);

                if (Encryption?.ClientCertificate != null)
                {
                    yield return new KeyValuePair<string, string>("clientCertificate", Encryption!.ClientCertificate);
                }

                if (Encryption?.ClientKey != null)
                {
                    yield return new KeyValuePair<string, string>("clientKey", Encryption!.ClientKey);
                }
            }
        }
    }

    /// <summary>
    /// A record for deserializing the response format from the management API to later turn into a <see cref="CouchbaseRemoteAnalyticsLink"/>
    /// </summary>
    internal record CouchbaseRemoteAnalyticsLinkResponse(
        [JsonProperty("name")]
        string Name,

        [JsonProperty("dataverse")]
        string? DataverseFromDataverse,

        [JsonProperty("scope")]
        string? DataverseFromScope,

        [JsonProperty("activeHostname")]
        string Hostname,

        [JsonProperty("encryption")]
        CouchbaseRemoteAnalyticsLink.EncryptionLevel Encryption
        ) : AnalyticsLinkResponseRecord(Name, DataverseFromDataverse, DataverseFromScope)
    {
        [JsonProperty("username")]
        public string? Username { get; init; }

        public CouchbaseRemoteAnalyticsLink AsRequest() => new CouchbaseRemoteAnalyticsLink(Name, DataverseFromEither, Hostname, new CouchbaseRemoteAnalyticsLink.EncryptionSettings(Encryption))
        {
            Username = Username,
            Password = null,
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
