#nullable enable
using System;
using System.Collections.Generic;
using Couchbase.Client.Transactions.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Client.Transactions.Components
{
    internal class AtrEntry
    {
        private static readonly IList<DocRecord> EmptyDocRecords = new List<DocRecord>().AsReadOnly();

        [JsonProperty(TransactionFields.AtrFieldTransactionId)]
        public string? TransactionId { get; set; }

        [JsonProperty(TransactionFields.AtrFieldStatus)]
        public AttemptStates State { get; set; }

        [JsonProperty(TransactionFields.AtrFieldStartTimestamp)]
        public string? TimestampStartCas { get; set; }

        [JsonIgnore]
        public DateTimeOffset? TimestampStartMsecs => ParseMutationCasField(TimestampStartCas);

        [JsonProperty(TransactionFields.AtrFieldStartCommit)]
        public string? TimestampCommitCas { get; }

        [JsonIgnore]
        public DateTimeOffset? TimestampCommitMsecs => ParseMutationCasField(TimestampCommitCas);

        [JsonProperty(TransactionFields.AtrFieldTimestampComplete)]
        public string? TimestampCompleteCas { get; set; }

        [JsonIgnore]
        public DateTimeOffset? TimestampCompleteMsecs => ParseMutationCasField(TimestampCompleteCas);

        [JsonProperty(TransactionFields.AtrFieldTimestampRollbackStart)]
        public string? TimestampRollBackCas { get; set; }

        [JsonIgnore]
        public DateTimeOffset? TimestampRollBackMsecs => ParseMutationCasField(TimestampRollBackCas);

        [JsonProperty(TransactionFields.AtrFieldTimestampRollbackComplete)]
        public string? TimestampRolledBackCas { get; set; }

        [JsonIgnore]
        public DateTimeOffset? TimestampRolledBackMsecs => ParseMutationCasField(TimestampRolledBackCas);

        [JsonProperty(TransactionFields.AtrFieldExpiresAfterMsecs)]
        public int? ExpiresAfterMsecs { get; set; }

        [JsonProperty(TransactionFields.AtrFieldDocsInserted)]
        public IList<DocRecord> InsertedIds { get; set; } = EmptyDocRecords;

        [JsonProperty(TransactionFields.AtrFieldDocsReplaced)]
        public IList<DocRecord> ReplacedIds { get; set; } = EmptyDocRecords;

        [JsonProperty(TransactionFields.AtrFieldDocsRemoved)]
        public IList<DocRecord> RemovedIds { get; set; } = EmptyDocRecords;

        [JsonProperty("fc")]
        public JObject? ForwardCompatibility { get; set; } = null;

        [JsonProperty("d")]
        public string? DurabilityLevel { get; set; } = null;

        public ulong? Cas { get; }

        public bool? IsExpired
        {
            get
            {
                if (TimestampStartMsecs == null || ExpiresAfterMsecs == null)
                {
                    return null;
                }

                return (TimestampStartMsecs.Value.AddMilliseconds(ExpiresAfterMsecs.Value) < DateTimeOffset.UtcNow);
            }
        }

        public static AtrEntry? CreateFrom(JToken entry)
        {
            _ = entry ?? throw new ArgumentNullException(nameof(entry));
            return entry.ToObject<AtrEntry>();
        }

        // ${Mutation.CAS} is written by kvengine with 'macroToString(htonll(info.cas))'.  Discussed this with KV team and,
        // though there is consensus that this is off (htonll is definitely wrong, and a string is an odd choice), there are
        // clients (SyncGateway) that consume the current string, so it can't be changed.  Note that only little-endian
        // servers are supported for Couchbase, so the 8 byte long inside the string will always be little-endian ordered.
        //
        // Looks like: "0x000058a71dd25c15"
        // Want:        0x155CD21DA7580000   (1539336197457313792 in base10, an epoch time in millionths of a second)
        internal static DateTimeOffset? ParseMutationCasField(string? casString)
        {
            if (string.IsNullOrWhiteSpace(casString))
            {
                return null;
            }

            int offsetIndex = 2; // for the initial "0x"
            long result = 0;

            for (int octetIndex = 7; octetIndex >= 0; octetIndex -= 1)
            {
                char char1 = casString![offsetIndex + (octetIndex * 2)];
                char char2 = casString[offsetIndex + (octetIndex * 2) + 1];

                long octet1 = 0;
                long octet2 = 0;

                if (char1 >= 'a' && char1 <= 'f')
                    octet1 = char1 - 'a' + 10;
                else if (char1 >= 'A' && char1 <= 'F')
                    octet1 = char1 - 'A' + 10;
                else if (char1 >= '0' && char1 <= '9')
                    octet1 = char1 - '0';
                else
                    throw new InvalidOperationException("Could not parse CAS " + casString);

                if (char2 >= 'a' && char2 <= 'f')
                    octet2 = char2 - 'a' + 10;
                else if (char2 >= 'A' && char2 <= 'F')
                    octet2 = char2 - 'A' + 10;
                else if (char2 >= '0' && char2 <= '9')
                    octet2 = char2 - '0';
                else
                    throw new InvalidOperationException("Could not parse CAS " + casString);

                result |= (octet1 << ((octetIndex * 8) + 4));
                result |= (octet2 << (octetIndex * 8));
            }

            // It's in millionths of a second
            var millis = (long)result / 1000000L;
            return DateTimeOffset.FromUnixTimeMilliseconds(millis);
        }

        internal static DateTimeOffset? ParseMutationCasFieldNoThrow(string? casString)
        {
            try
            {
                return ParseMutationCasField(casString);
            }
            catch
            {
                return null;
            }
        }

    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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







