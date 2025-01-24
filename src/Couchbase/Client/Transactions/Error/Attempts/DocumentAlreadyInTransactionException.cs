using Couchbase.Core.Retry;

namespace Couchbase.Client.Transactions.Error.Attempts
{
    /// <summary>
    /// Indicates a document being modified is already being modified in the same transaction.
    /// </summary>
    public class DocumentAlreadyInTransactionException : AttemptException, IRetryable
    {
        /// <summary>
        /// Gets the document in question.
        /// </summary>
        public TransactionGetResult Doc { get; }

        private DocumentAlreadyInTransactionException(AttemptContext ctx, TransactionGetResult doc, string msg)
            : base(ctx, msg)
        {
            Doc = doc;
        }

        /// <summary>
        /// Creates an instance of the DocumentAlreadyInTransactionException.
        /// </summary>
        /// <param name="ctx">The AttemptContext.</param>
        /// <param name="doc">The document in question.</param>
        /// <returns>An initialized instance.</returns>
        public static DocumentAlreadyInTransactionException Create(AttemptContext ctx, TransactionGetResult doc)
        {
            var msg =
                $"Document {ctx.Redactor.UserData(doc.Id)} is already in a transaction, atr={doc.TransactionXattrs?.AtrRef?.ToString()}, attemptId = {doc.TransactionXattrs?.Id?.AttemptId ?? "-"}";

            return new DocumentAlreadyInTransactionException(ctx, doc, msg);
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
