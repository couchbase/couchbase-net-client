using Couchbase.Core.Exceptions;
using Couchbase.Core.Retry;
using Couchbase.Query;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.External;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Transactions.Internal
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

        private class QueryErrorHandlingEnumeratorWrapper<T> : IAsyncEnumerator<T>
        {
            private readonly IAsyncEnumerator<T> _enumerator;
            private readonly AttemptContext _ctx;

            public QueryErrorHandlingEnumeratorWrapper(IAsyncEnumerator<T> enumerator, AttemptContext ctx)
            {
                _enumerator = enumerator;
                _ctx = ctx;
            }

            public T Current => _enumerator.Current;

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
                        Exception toRaise = err.FinalErrorToRaise switch
                        {
                            TransactionOperationFailedException.FinalError.TransactionFailed => new TransactionFailedException("Failed during query results streaming", err, null),
                            TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous => new TransactionCommitAmbiguousException("Ambiguous commit during single query transaction", err, null),
                            TransactionOperationFailedException.FinalError.TransactionExpired => new UnambiguousTimeoutException("Timeout during query result streaming"),
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
