using System;

#nullable enable

namespace Couchbase.Core.Exceptions.Query
{
    public abstract class QueryException : CouchbaseException<IQueryErrorContext>
    {
        protected QueryException() {}

        protected QueryException(IQueryErrorContext context) : base(context) {}

        protected QueryException(string message) : base(message) {}

        protected QueryException(string message, Exception? innerException) : base(message, innerException) {}
    }
}
