#nullable enable
using System;
using System.Collections.Generic;


namespace Couchbase.Client.Transactions.Config
{
    public class TransactionCleanupConfig
    {
        /// <summary>
        /// The default cleanup window, in milliseconds.
        /// </summary>
        /// <seealso cref="TransactionCleanupConfig.CleanupWindow"/>
        public const int DefaultCleanupWindowMilliseconds = 60_000;

        /// <summary>
        /// The default value of <see cref="TransactionCleanupConfig.CleanupLostAttempts"/> (true).
        /// </summary>
        public const bool DefaultCleanupLostAttempts = true;

        /// <summary>
        /// The default value of <see cref="TransactionCleanupConfig.CleanupClientAttempts"/> (true).
        /// </summary>
        public const bool DefaultCleanupClientAttempts = true;
        /// <summary>
        /// Gets a value indicating whether to run the background thread to clean up lost/abandoned transactions from other clients.
        /// </summary>
        public bool CleanupLostAttempts { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether to run the background thread to clean up failed attempts from this transactions instance.
        /// </summary>
        public bool CleanupClientAttempts { get; internal set; }

        /// <summary>
        /// Gets a value indicating the period between runs of the cleanup thread.
        /// </summary>
        public TimeSpan CleanupWindow { get; internal set; }

        /// <summary>
        /// Collections to cleanup lost/abandoned transactions attempts from this or other clients
        /// </summary>
        public List<Keyspace> CollectionsList { get; internal set; }

        internal TransactionCleanupConfig(
            TimeSpan? cleanupWindow = null,
            bool cleanupClientAttempts = DefaultCleanupClientAttempts,
            bool cleanupLostAttempts = DefaultCleanupLostAttempts,
            List<Keyspace>? collectionsList = null
        )
        {
            CleanupLostAttempts = cleanupLostAttempts;
            CleanupClientAttempts = cleanupClientAttempts;
            CleanupWindow = cleanupWindow ?? TimeSpan.FromMilliseconds(DefaultCleanupWindowMilliseconds);
            CollectionsList = collectionsList ?? [];
        }
    }
}
