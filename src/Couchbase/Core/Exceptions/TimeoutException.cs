using System;

namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// The parent timeout exception of <see cref="AmbiguousTimeoutException"/> and <see cref="UnambiguousTimeoutException"/>.
    /// </summary>
    public class TimeoutException : CouchbaseException
    {
        public TimeoutException() { }

        public TimeoutException(IErrorContext context) : base(context.Message)
        {
            Context = context;
        }

        public TimeoutException(string message) : base(message) { }

        public TimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }
}
