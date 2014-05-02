using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Generic exception thrown when a configuration cannot be bootstrapped or is the wrong type for the given bucket.
    /// </summary>
    public class ConfigException : Exception
    {
        public ConfigException()
        {
        }

        public ConfigException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ConfigException(string message) : base(message)
        {
        }

        public ConfigException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        protected ConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
