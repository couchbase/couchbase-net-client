using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Thrown when a configuration does not exist within the provider.
    /// </summary>
    public class ConfigNotFoundException : Exception
    {
        public ConfigNotFoundException()
        {
        }

        public ConfigNotFoundException(string message) : base(message)
        {
        }

        public ConfigNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ConfigNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
