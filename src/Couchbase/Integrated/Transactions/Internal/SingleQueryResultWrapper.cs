#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Retry;
using Couchbase.Integrated.Transactions.Error;
using Couchbase.Integrated.Transactions.Error.External;
using Couchbase.Query;

namespace Couchbase.Integrated.Transactions.Internal
{
    internal class SingleQueryResultWrapper<T> : IQueryResult<T>
    {
        private readonly IQueryResult<T> _wrapped;
        private readonly AttemptContext _ctx;

        public SingleQueryResultWrapper(IQueryResult<T> wrapped, AttemptContext ctx)
        {
            _wrapped = wrapped;
            _ctx = ctx;
        }

        public IAsyncEnumerable<T> Rows => this;

        public QueryMetaData? MetaData => _wrapped.MetaData;

        public List<Query.Error> Errors => _wrapped.Errors;

        public RetryReason RetryReason => _wrapped.RetryReason;

        public void Dispose() => _wrapped.Dispose();

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new QueryErrorHandlingEnumeratorWrapper<T>(_wrapped.GetAsyncEnumerator(cancellationToken), _ctx);

        private class QueryErrorHandlingEnumeratorWrapper<TItem> : IAsyncEnumerator<TItem>
        {
            private readonly IAsyncEnumerator<TItem> _enumerator;
            private readonly AttemptContext _ctx;

            public QueryErrorHandlingEnumeratorWrapper(IAsyncEnumerator<TItem> enumerator, AttemptContext ctx)
            {
                _enumerator = enumerator;
                _ctx = ctx;
            }

            public TItem Current => _enumerator.Current;

            public ValueTask DisposeAsync() => _enumerator.DisposeAsync();

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    var result = await _enumerator.MoveNextAsync().CAF();
                    return result;
                }
                catch (Exception ex)
                {
                    var converted = _ctx.ConvertQueryError(ex);
                    if (converted is TransactionOperationFailedException err)
                    {
                        Exception toRaise = err.ToRaise switch
                        {
                            TransactionOperationFailedException.FinalErrorToRaise.TransactionFailed => new TransactionFailedException("Failed during query results streaming", err, null),
                            TransactionOperationFailedException.FinalErrorToRaise.TransactionCommitAmbiguous => new TransactionCommitAmbiguousException("Ambiguous commit during single query transaction", err, null),
                            TransactionOperationFailedException.FinalErrorToRaise.TransactionExpired => new UnambiguousTimeoutException("Timeout during query result streaming"),
                            _ => err
                        };

                        throw toRaise;
                    }

                    if (converted != null)
                    {
                        throw converted;
                    }

                    throw;
                }
            }
        }
    }
}





