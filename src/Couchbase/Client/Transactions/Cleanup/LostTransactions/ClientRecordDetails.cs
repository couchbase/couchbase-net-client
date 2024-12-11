#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Client.Transactions.DataModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Client.Transactions.Cleanup.LostTransactions
{
    internal class ClientRecordDetails
    {
        public const long NanosecondsPerMillisecond = 1_000_000;
        public IReadOnlyList<string> SortedActiveClientIds { get; }
        public IReadOnlyList<string> ExpiredClientIds { get; }
        public IReadOnlyList<string> ActiveClientIds { get; }

        [JsonIgnore]
        public IReadOnlyList<string> AtrsHandledByThisClient { get; }
        public TimeSpan CheckAtrTimeWindow { get; }

        public int NumActiveClients => ActiveClientIds.Count;
        public int NumExpiredClients => ExpiredClientIds.Count;
        public int NumExistingClients => NumActiveClients + NumExpiredClients;

        public int IndexOfThisClient { get; } = -1;
        public bool OverrideEnabled { get; } = false;
        public DateTimeOffset? OverrideExpires { get; }
        public bool OverrideActive { get; }

        public long CasNowNanos { get; }

        public ClientRecordDetails(ClientRecordsIndex clientRecord, ParsedHLC parsedHlc, string clientUuid, TimeSpan cleanupWindow)
        {
            _ = clientRecord ?? throw new ArgumentNullException(nameof(clientRecord));
            _ = parsedHlc ?? throw new ArgumentNullException(nameof(parsedHlc));
            CasNowNanos = parsedHlc.NowTime.ToUnixTimeMilliseconds() * NanosecondsPerMillisecond;
            OverrideEnabled = clientRecord.Override?.Enabled == true;
            OverrideExpires = clientRecord.Override?.Expires;
            OverrideActive = OverrideEnabled && parsedHlc.NowTime < OverrideExpires;
            var clientCount = clientRecord.Clients?.Count ?? 0;
            var expiredClientIds = new List<string>(clientCount);
            var activeClientIds = new List<string>(clientCount);
            bool thisClientAlreadyExists = false;
            if (clientCount > 0)
            {
                foreach (var kvp in clientRecord.Clients!)
                {
                    var uuid = kvp.Key;
                    var client = kvp.Value;
                    var parsedHlcNow = parsedHlc.NowTime;

                    // (Note, do not include this client as expired, as it is about to add itself)
                    if (uuid == clientUuid)
                    {
                        activeClientIds.Add(uuid);
                        thisClientAlreadyExists = true;
                    }
                    else if (client.ParsedMutationCas == null || client.Expires < parsedHlcNow)
                    {
                        expiredClientIds.Add(uuid);
                    }
                    else
                    {
                        activeClientIds.Add(uuid);
                    }
                }
            }

            if (!thisClientAlreadyExists)
            {
                activeClientIds.Add(clientUuid);
            }

            var sortedActiveClientIds = activeClientIds.ToList();
            sortedActiveClientIds.Sort();
            SortedActiveClientIds = sortedActiveClientIds;
            for (int i = 0; i < SortedActiveClientIds.Count; i++)
            {
                if (SortedActiveClientIds[i] == clientUuid)
                {
                    IndexOfThisClient = i;
                    break;
                }
            }

            ExpiredClientIds = expiredClientIds.ToList();
            ActiveClientIds = activeClientIds.ToList();
            AtrsHandledByThisClient = GetAtrsHandledByThisClient().ToList();
            var handledCount = AtrsHandledByThisClient.Count;
            handledCount = handledCount == 0 ? 1 : handledCount;
            CheckAtrTimeWindow = TimeSpan.FromMilliseconds(cleanupWindow.TotalMilliseconds / handledCount);
        }

        private IEnumerable<string> GetAtrsHandledByThisClient()
        {
            if (IndexOfThisClient < 0)
            {
                yield break;
            }

            foreach (var atr in ActiveTransactionRecords.AtrIds.Nth(IndexOfThisClient, NumActiveClients))
            {
                yield return atr;
            }
        }

        public override string ToString()
        {
            return JObject.FromObject(this).ToString();
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







