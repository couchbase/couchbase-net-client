#nullable enable
using System;
using System.Collections.Generic;

namespace Couchbase.Client.Transactions.Config
{
    public class TransactionCleanupConfigBuilder
    {
        private readonly TransactionCleanupConfig _config;

        private TransactionCleanupConfigBuilder()
        {
            _config = new TransactionCleanupConfig();
        }
        /// <summary>
        /// Create an instance of the config.
        /// </summary>
        /// <returns>An instance of the <see cref="TransactionCleanupConfigBuilder"/>.</returns>
        public static TransactionCleanupConfigBuilder Create() => new();

        /// <summary>
        /// Each client that has cleanupLostAttempts(true) enabled, will be participating in the distributed cleanup process.
        /// This involves checking all ATRs every cleanup window, and this parameter controls the length of that window.
        /// </summary>
        /// <param name="cleanupWindow">The length of the cleanup window.</param>
        /// <returns>The builder.</returns>
        public TransactionCleanupConfigBuilder CleanupWindow(TimeSpan cleanupWindow)
        {
            _config.CleanupWindow = cleanupWindow;
            return this;
        }

        /// <summary>
        /// Controls where any transaction attempts made by this client are automatically removed.
        /// </summary>
        /// <param name="cleanupClientAttempts">Whether to clean up attempts made by this client.</param>
        /// <returns>The builder.</returns>
        public TransactionCleanupConfigBuilder CleanupClientAttempts(bool cleanupClientAttempts)
        {
            _config.CleanupClientAttempts = cleanupClientAttempts;
            return this;
        }

        /// <summary>
        /// Controls where a background process is created to clean up any 'lost' transaction attempts.
        /// </summary>
        /// <param name="cleanupLostAttempts">Whether to clean up lost attempts from other clients.</param>
        /// <returns>The builder.</returns>
        public TransactionCleanupConfigBuilder CleanupLostAttempts(bool cleanupLostAttempts)
        {
            _config.CleanupLostAttempts = cleanupLostAttempts;
            return this;
        }

        /// <summary>
        /// Generate a <see cref="TransactionCleanupConfig"/> from the values provided.
        /// </summary>
        /// <returns>A <see cref="TransactionCleanupConfig"/> that has been initialized with the given values.</returns>
        public TransactionCleanupConfig Build() => _config;

        /// <summary>
        /// Add a collection to be cleaned of lost/abandoned transactions by this or other transaction clients
        /// </summary>
        /// <param name="keyspace"></param>
        /// <returns></returns>
        public TransactionCleanupConfigBuilder AddCollection(Keyspace keyspace)
        {
            _config.CollectionsList.Add(keyspace);
            return this;
        }

    }
}

