using System;
using System.Threading;
using Couchbase.Transactions.Error.Internal;

namespace Couchbase.Transactions.Error.External
{
    /// <summary>
    /// Indicates an operation in a transaction failed.
    /// </summary>
    public class TransactionOperationFailedException : CouchbaseException, IClassifiedTransactionError
    {
        /// <summary>
        /// Placeholder for "no failure".
        /// </summary>
        public const TransactionOperationFailedException? None = null;

        private static long ExceptionCount = 0;

        /// <summary>
        /// Gets the Exception Number.
        /// </summary>
        public long ExceptionNumber { get; }

        /// <summary>
        /// Gets the general class of error that caused the exception.
        /// </summary>
        public ErrorClass CausingErrorClass { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction attempt should automatically be rolled back.
        /// </summary>
        public bool AutoRollbackAttempt { get; }

        /// <summary>
        /// Gets a value indicating whether this transaction can be retried or not.
        /// </summary>
        public bool RetryTransaction { get; }

        /// <summary>
        /// Gets the exception that caused the failure.
        /// </summary>
        public Exception Cause { get; }

        /// <summary>
        /// Gets the final error to raise if this is the last attempt in the transaction.
        /// </summary>
        public FinalError FinalErrorToRaise { get; }

        /// <summary>
        /// An enumeration of the final error types that can fail a transaction.
        /// </summary>
        public enum FinalError
        {
            /// <summary>
            /// Generic failure.
            /// </summary>
            TransactionFailed = 0,

            /// <summary>
            /// The transaction expired.
            /// </summary>
            TransactionExpired = 1,

            /// <summary>
            /// An error occured in a way that the client cannot know whether the commit was successful or not.
            /// </summary>
            TransactionCommitAmbiguous = 2,

            /**
             * This will currently result in returning success to the application, but unstagingCompleted() will be false.
             */
            TransactionFailedPostCommit = 3
        }

        /// <inheritdoc />
        public TransactionOperationFailedException(
            ErrorClass causingErrorClass,
            bool autoRollbackAttempt,
            bool retryTransaction,
            Exception cause,
            FinalError finalErrorToRaise)
        {
            ExceptionNumber = Interlocked.Increment(ref ExceptionCount);
            CausingErrorClass = causingErrorClass;
            AutoRollbackAttempt = autoRollbackAttempt;
            RetryTransaction = retryTransaction;
            Cause = cause;
            FinalErrorToRaise = finalErrorToRaise;
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
