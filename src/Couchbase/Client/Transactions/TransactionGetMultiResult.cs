#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Core.Exceptions;

namespace Couchbase.Client.Transactions;

public class TransactionGetMultiResult : TransactionGetMultiResultBase
{
    internal TransactionGetMultiResult(int numSpecs) : base(numSpecs)
    {}
}

public class
    TransactionGetMultiReplicaFromPreferredServerGroupResult : TransactionGetMultiResultBase
{
    internal TransactionGetMultiReplicaFromPreferredServerGroupResult(int numSpecs) : base(numSpecs) {}
}

// Currently, the response from both GetMulti and GetMultiFromPreferredServerGroup are identical, so
// we use a common base.   We may let these diverge later.
public class TransactionGetMultiResultBase
{
    // array since we know in advance the size, and we don't have to lock it since one
    // thread per index will write.
    private readonly GetMultiSpecResult[] _specResults;

    // By default, we use pre-commit document.   If this is false, we go with post-commit if there
    // are transaction xattrs in the doc.
    private bool _usePreCommit = true;


    /// <summary>
    /// Return content of the result specified by index, as a T.
    /// </summary>
    /// <param name="specIndex">Index of the result (which matches the index of the specs).</param>
    /// <typeparam name="T">Return type desired</typeparam>
    /// <returns></returns>
    public T? ContentAs<T>(int specIndex)
    {
        // review what to do if the result is null.   We return
        // default now.
        CheckIndex(specIndex);
        CheckFetched(specIndex);
        var result = _specResults[specIndex].Result;
        if (result == null) return default;

        if (result.TransactionXattrs != null && !_usePreCommit)
        {
            return result.GetPostTransactionResult().ContentAs<T>();
        }
        return result.GetPreTransactionResult().ContentAs<T>();
    }

    /// <summary>
    /// Check for the existence of a document at the specified index.  Nice to do before calling
    /// ContentAs, just to avoid confusion.
    /// </summary>
    /// <param name="specIndex">Index of the result (which matches the index of the specs).</param>
    /// <returns>True if the document exists, False otherwise</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the index supplied is out of bounds</exception>
    public bool Exists(int specIndex)
    {
        CheckIndex(specIndex);
        CheckFetched(specIndex);
        return _specResults[specIndex].Result != null;
    }

    internal bool AllFetched()
    {
        return _specResults.All(result =>
            result.State != Client.Transactions.GetMultiSpecResult.DocState.ToFetch);
    }

    // In FetchDocuments stage, only fetch those with ToFetch State.
    internal bool ShouldFetch(int specIndex) =>
        _specResults[specIndex].State == Client.Transactions
            .GetMultiSpecResult.DocState.ToFetch;

    internal void ResetResults()
    {
        // reset everything.
        _usePreCommit = true; // that's the default.
        for (var i = 0; i < _specResults.Length; i++)
        {
            // initially all are created with the default constructor
            // so they have state ToFetch, and null DocumentLookupResult
            _specResults[i] = new GetMultiSpecResult(this, i);
        }
    }

    internal int CountUniqueTransactionsInResults(string excludeTransactionId)
    {
        var transactionIds = new HashSet<string>();
        foreach (var result in _specResults)
        {
            var id = result.Result?.TransactionXattrs?.Id?.Transactionid;
            if (id == null) continue;
            transactionIds.Add(id);
        }
        transactionIds.Remove(excludeTransactionId);
        return transactionIds.Count;
    }

    internal void UsePostCommit()
    {
        _usePreCommit = false;
    }

    internal void UsePreCommit()
    {
        _usePreCommit = true;
    }

    internal (AtrRef, CompositeId) GetFirstAtrRef(string excludeTransactionId)
    {
        foreach (var result in _specResults)
        {
            if (result.Result?.TransactionXattrs?.Id?.Transactionid != null &&
                result.Result?.TransactionXattrs?.Id?.Transactionid != excludeTransactionId &&
                result.Result?.TransactionXattrs?.AtrRef != null &&
                result.Result?.TransactionXattrs.Id != null)
            {
                // ok we found the first doc with txn metadata not in our own txn...
                return (result.Result!.TransactionXattrs!.AtrRef,
                    result.Result!.TransactionXattrs!.Id);
            }
        }
        throw new InvalidArgumentException("Atr not found");
    }
    internal TransactionGetMultiResultBase(int numResults)
    {
        _specResults = new GetMultiSpecResult[numResults];
        for (var i = 0; i < numResults; i++)
        {
            // initially all are created with the primary constructor
            // so they have state ToFetch, and null DocumentLookupResult,
            // but know the parent and their index within it.
            _specResults[i] = new GetMultiSpecResult(this, i);
        }
    }

    // completely safe if one thread per index writes.
    internal void InsertResult(DocumentLookupResult? result, int specIndex)
    {
        CheckIndex(specIndex);

        _specResults[specIndex].Result = result;
    }

    internal void InsertSignal(Signal signal, int specIndex)
    {
        CheckIndex(specIndex);
        _specResults[specIndex].Signal = signal;
    }

    internal Signal? GetFirstSignal()
    {
        foreach (var result in _specResults)
        {
            if (result.Signal != null) return result.Signal;
        }

        return null;
    }

    private void CheckIndex(int index)
    {
        if (index < 0 || index >= _specResults.Length)
            throw new IndexOutOfRangeException($"Spec index {index} is out of range.");
    }

    private void CheckFetched(int index)
    {
        if (_specResults[index].State == Client.Transactions.GetMultiSpecResult.DocState.ToFetch)
            throw new Exception("Document not fetched yet.");
    }

    internal DocumentLookupResult? FindFirstResult(Func<DocumentLookupResult?, bool> predicate)
    {
        foreach (var result in _specResults)
        {
            if (predicate(result.Result))
                return result.Result;
        }

        return null;
    }
    internal void IterateResults(Func<DocumentLookupResult?, bool> whereFunc,
        Action<DocumentLookupResult?, int> actionFunc)
    {
        for(var idx = 0; idx < _specResults.Length; idx++)
        {
            var res = _specResults[idx].Result;
            if (whereFunc(res))
                actionFunc(res, idx);
        }
    }

    internal GetMultiSpecResult GetMultiSpecResult(int specIndex)
    {
        return _specResults[specIndex];
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

