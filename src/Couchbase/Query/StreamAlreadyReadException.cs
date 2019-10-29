using System;

namespace Couchbase.Query
{
    /// <summary>
    /// Thrown when an attempt is made to access a property or methods before reading the request stream via iteration.
    /// </summary>
    /// <seealso cref="InvalidOperationException" />
    public class StreamAlreadyReadException : InvalidOperationException
    {
        private const string DefaultMessage = "The underlying stream has already been read and cannot be read again.";

        public StreamAlreadyReadException(string message = DefaultMessage, Exception exception = null)
            : base(message, exception)
        { }
    }
}
