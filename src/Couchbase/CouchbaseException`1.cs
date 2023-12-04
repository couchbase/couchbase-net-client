using System;
using Couchbase.Core;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Base exception for all exceptions generated or handled by the Couchbase SDK.
    /// </summary>
    public abstract class CouchbaseException<TContext> : CouchbaseException
        where TContext : class, IErrorContext
    {
        protected CouchbaseException() {}

        protected CouchbaseException(IErrorContext context) : base(context) {}

        protected CouchbaseException(TContext context) : base(context) {}

        protected CouchbaseException(string message) : base(message) {}

        protected CouchbaseException(string message, Exception? innerException) : base(message, innerException) {}

        /// <summary>
        /// Additional context about the <see cref="CouchbaseException"/>.
        /// </summary>
        public new TContext? Context
        {
            get => base.Context as TContext;
            set => base.Context = value;
        }
    }
}
