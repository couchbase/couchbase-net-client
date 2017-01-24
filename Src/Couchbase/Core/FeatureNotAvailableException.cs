using System;
using System.Runtime.Serialization;

namespace Couchbase.Core
{
    /// <summary>
    /// Thrown when an application makes a subdoc request targeting an XAttribute when the cluster does not support it.
    /// </summary>
    public class FeatureNotAvailableException : Exception
    {
        public FeatureNotAvailableException()
        { }

        public FeatureNotAvailableException(string message)
            : base(message)
        { }

        public FeatureNotAvailableException(string message, Exception innerException)
            : base(message, innerException)
        { }

#if NET45
        protected FeatureNotAvailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}
