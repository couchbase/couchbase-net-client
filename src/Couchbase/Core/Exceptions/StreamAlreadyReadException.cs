using System;

namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// Thrown when an attempt is made to read a stream of results more than once.
    /// </summary>
    public class StreamAlreadyReadException : CouchbaseException
    {
        private const string DefaultMessage = "The underlying stream has already been read and cannot be read again.";

        public StreamAlreadyReadException()
            : this(DefaultMessage)
        { }

        public StreamAlreadyReadException(string message)
            : base(message)
        { }

        public StreamAlreadyReadException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
