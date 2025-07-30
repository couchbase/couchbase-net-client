#nullable enable
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;

namespace Couchbase.Client.Transactions;
// this represents the DocumentLookupResult, with state specific to the GetMulti call
internal class GetMultiSpecResult(TransactionGetMultiResultBase parent, int specIndex)
{
    private DocumentLookupResult? _result;
    private Signal? _signal;

    // we use this when we initially create the result, before we ever get the
    // actual content (which, of course, could be null).   We only really need
    // the specIndex in tests, where we process the results, so we can pass this
    // and do a ContentAs<T>.
    public enum DocState
    {
        ToFetch,
        AlreadyFetched,
        WereInT1
    }
    public Signal? Signal
    {
        get => _signal;
        set
        {
            _signal = value;
            _result = null;
        }
    }

    public DocumentLookupResult? Result
    {
        get => _result;
        set
        {
            _result = value;
            State = DocState.AlreadyFetched;
            _signal = null;
        }
    }

    public DocState State { get; set; } = DocState.ToFetch;

    public T? ContentAs<T>()
    {
        // we will ask through the parent as we may want pre or post txn content, depending
        // on circumstances.
        return parent.ContentAs<T>(specIndex);
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
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

