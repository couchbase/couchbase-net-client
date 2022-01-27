using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Transactions.Support
{
    internal static class TransactionFields
    {
        // Fields in the Active Transaction Records
        // These are keep as brief as possible, more important to reduce changes of doc overflowing
        // than to preserve human ease of debugging
        public const string AtrFieldAttempts = "attempts";
        public const string AtrFieldTransactionId = "tid";
        public const string AtrFieldStatus = "st";
        public const string AtrFieldStartTimestamp = "tst";
        public const string AtrFieldExpiresAfterMsecs = "exp";
        public const string AtrFieldStartCommit = "tsc";
        public const string AtrFieldTimestampComplete = "tsco";
        public const string AtrFieldTimestampRollbackStart = "tsrs";
        public const string AtrFieldTimestampRollbackComplete = "tsrc";
        public const string AtrFieldDocsInserted = "ins";
        public const string AtrFieldDocsReplaced = "rep";
        public const string AtrFieldDocsRemoved = "rem";
        public const string AtrFieldPerDocId = "id";
        public const string AtrFieldPerDocBucket = "bkt";
        public const string AtrFieldPerDocScope = "scp";
        public const string AtrFieldPerDocCollection = "col";
        public const string AtrFieldPendingSentinel = "p";
        public const string AtrFieldDurability = "d";

        // Fields inside regular docs that are part of a transaction
        public const string TransactionInterfacePrefixOnly = "txn";
        public const string TransactionInterfacePrefix = TransactionInterfacePrefixOnly + ".";
        public const string TransactionRestorePrefixOnly = TransactionInterfacePrefixOnly + ".restore";
        public const string TransactionRestorePrefix = TransactionRestorePrefixOnly + ".";
        public const string TransactionId = TransactionInterfacePrefix + "id.txn";
        public const string AttemptId = TransactionInterfacePrefix + "id.atmpt";
        public const string AtrId = TransactionInterfacePrefix + "atr.id";
        public const string AtrBucketName = TransactionInterfacePrefix + "atr.bkt";

        public const string AtrScopeName = TransactionInterfacePrefix + "atr.scp";
        // The current plan is:
        // 6.5 and below: write metadata docs to the default collection
        // 7.0 and above: write them to the system collection, and migrate them over
        // Adding scope and collection metadata fields to try and future proof
        public const string AtrCollName = TransactionInterfacePrefix + "atr.coll";
        public const string StagedData = TransactionInterfacePrefix + "op.stgd";
        public const string Type = TransactionInterfacePrefix + "op.type";
        public const string Crc32 = TransactionInterfacePrefix + "op.crc32";

        public const string PreTxnCas = TransactionRestorePrefix + "CAS";
        public const string PreTxnRevid = TransactionRestorePrefix + "revid";
        public const string PreTxnExptime = TransactionRestorePrefix + "exptime";

        public const string StagedDataRemoveKeyword ="<<REMOVE>>";
        public static readonly byte[] StagedDataRemoveKeywordBytes = Encoding.UTF8.GetBytes(StagedDataRemoveKeyword);
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
