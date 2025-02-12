#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Client.Transactions.Config;

namespace Couchbase.Client.Transactions
{
    internal class TransactionContext
    {
        private readonly ConcurrentQueue<string> _logs = new ConcurrentQueue<string>();

        public string TransactionId { get; }
        public DateTimeOffset StartTime { get; }
        public MergedTransactionConfig Config { get; }

        public DateTimeOffset AbsoluteExpiration => StartTime + Config.ExpirationTime;
        public bool IsExpired => AbsoluteExpiration <= DateTimeOffset.UtcNow;

        public TimeSpan RemainingUntilExpiration => AbsoluteExpiration - DateTimeOffset.UtcNow;

        public TransactionContext(
            string transactionId,
            DateTimeOffset startTime,
            TransactionsConfig config,
            PerTransactionConfig? perConfig)
        {
            TransactionId = transactionId;
            StartTime = startTime;
            Config = MergedTransactionConfig.Create(config, perConfig);
        }

        internal void AddLog(string msg) => _logs.Enqueue(msg);

        internal IEnumerable<string> Logs => _logs.ToArray();
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
