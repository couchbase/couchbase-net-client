#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Couchbase.Client.Transactions.DataModel
{
    /// <summary>
    /// A model class for JSON serialization/deserialization of the root Client Records object.
    /// </summary>
    internal record ClientRecordsIndex
    {
        public const string CLIENT_RECORD_DOC_ID = "_txn:client-record";
        public const string VBUCKET_HLC = "$vbucket.HLC";
        public const string FIELD_RECORDS = "records";
        public const string FIELD_CLIENTS = "clients";
        public const string FIELD_CLIENTS_FULL = FIELD_RECORDS + "." + FIELD_CLIENTS;
        public const string FIELD_OVERRIDE = "override";

        [JsonProperty(FIELD_CLIENTS)]
        [JsonPropertyName(FIELD_CLIENTS)]
        public Dictionary<string, ClientRecordEntry> Clients { get; init; } = new Dictionary<string, ClientRecordEntry>();

        [JsonProperty(FIELD_OVERRIDE)]
        [JsonPropertyName(FIELD_OVERRIDE)]
        public ClientRecordsOverride? Override { get; init; } = null;
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
