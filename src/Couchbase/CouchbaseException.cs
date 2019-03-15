using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Couchbase
{
    /// <summary>
    /// Base exception for all exceptions generated or handled by the Couchbase SDK.
    /// </summary>
    public class CouchbaseException : Exception
    {
        public CouchbaseException()
        {
        }

        public CouchbaseException(string message) : base(message)
        {
        }

        public CouchbaseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
