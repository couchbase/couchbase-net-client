using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Thrown when an observer for a given configuration cannot be found.
    /// </summary>
    public class ConfigObserverNotFoundException : Exception
    {
        public ConfigObserverNotFoundException()
        {
        }

        public ConfigObserverNotFoundException(string message) : base(message)
        {
        }

        public ConfigObserverNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ConfigObserverNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
