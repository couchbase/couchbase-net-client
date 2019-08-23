using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when the clusterOptions is invalid.
    /// </summary>
    public class InvalidConfigurationException : CouchbaseException
    {
        public  InvalidConfigurationException()
        {
        }

        public InvalidConfigurationException(string message)
            : base(message)
        {
        }

        public InvalidConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }

}
