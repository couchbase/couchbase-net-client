using System.Globalization;
using Couchbase.Core;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Support;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions.Components
{
    internal class StagedMutation
    {
        public TransactionGetResult Doc { get; }
        public object? Content { get; }
        public StagedMutationType Type { get; }
        public MutationToken MutationToken { get; }

        public StagedMutation(TransactionGetResult doc, object? content, StagedMutationType type, MutationToken mutationToken)
        {
            Doc = doc;
            Content = content;
            Type = type;
            MutationToken = mutationToken;
        }

        public JObject ForAtr() => new JObject(
            new JProperty(TransactionFields.AtrFieldPerDocId, Doc.Id),
            new JProperty(TransactionFields.AtrFieldPerDocBucket, Doc.Collection.Scope.Bucket.Name),
            new JProperty(TransactionFields.AtrFieldPerDocScope, Doc.Collection.Scope.Name),
            new JProperty(TransactionFields.AtrFieldPerDocCollection, Doc.Collection.Name)
        );

        public DocRecord AsDocRecord() => new DocRecord(
            bkt: Doc.Collection.Scope.Bucket.Name,
            scp: Doc.Collection.Scope.Name,
            col: Doc.Collection.Name,
            id: Doc.Id);

        public TxDataMutation AsTxData() => new TxDataMutation(
            scp: Doc.Collection.Scope.Name,
            coll: Doc.Collection.Name,
            bkt: Doc.Collection.Scope.Bucket.Name,
            id: Doc.Id,
            cas: Doc.Cas.ToString(CultureInfo.InvariantCulture),
            type: Type.ToString().ToUpperInvariant()
            );
    }

    internal enum StagedMutationType
    {
        Undefined = 0,
        Insert = 1,
        Remove = 2,
        Replace = 3
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
