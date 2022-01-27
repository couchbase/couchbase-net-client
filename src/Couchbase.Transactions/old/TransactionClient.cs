using System;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Transactions.old.Config;
using Polly;

namespace Couchbase.Transactions.old
{
    public class TransactionClient : ITransactionClient
    {
        private readonly ICluster _cluster;
        private readonly DateTime _startedAt;
        private readonly Policy _policy;

        public TransactionConfig Config { get; }
        public TimeSpan Duration => DateTime.UtcNow.Subtract(_startedAt);

        public TransactionClient(ICluster cluster, TransactionConfig config)
        {
            _cluster = cluster;
            _startedAt = DateTime.UtcNow;
            Config = config;

            // setup retry policy using Polly
            //TODO: handle retryable exceptions differently
            _policy = Policy.Handle<Exception>().Retry(Config.MaxAttempts);
        }

        public async Task<ITransactionResult> Run(Func<IAttemptContext, Task> transactionLogic)
        {
            var transactionContext = new TransactionContext();

            await _policy.Execute(async () =>
            {
                var attemptContext = new AttemptContext(transactionContext.TransactionId, Config);
                await transactionLogic(attemptContext).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return new TransactionResult(transactionContext.TransactionId, transactionContext.Attempts, Duration);
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
