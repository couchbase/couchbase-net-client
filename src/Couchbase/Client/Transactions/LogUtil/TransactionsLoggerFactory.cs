#nullable enable
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.LogUtil
{
    internal class TransactionsLoggerFactory : ILoggerFactory
    {
        private readonly ILoggerFactory _otherLoggerFactory;
        private readonly TransactionContext _overallContext;

        public TransactionsLoggerFactory(ILoggerFactory otherLoggerFactory, TransactionContext overallContext)
        {
            _otherLoggerFactory = otherLoggerFactory;
            _overallContext = overallContext;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            _otherLoggerFactory.AddProvider(provider);
        }

        public ILogger CreateLogger(string categoryName) => new TransactionsLogger(_otherLoggerFactory.CreateLogger(categoryName), _overallContext);

        public void Dispose()
        {
            _otherLoggerFactory.Dispose();
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





