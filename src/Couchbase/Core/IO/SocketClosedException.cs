using System;

namespace Couchbase.Core.IO
{
    public class SocketClosedException : CouchbaseException
    {
        public SocketClosedException()
        {
        }

        public SocketClosedException(string message)
            : base(message)
        {
        }

        public SocketClosedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
