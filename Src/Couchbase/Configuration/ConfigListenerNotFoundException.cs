using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration
{
    public class ConfigListenerNotFoundException : Exception
    {
        public ConfigListenerNotFoundException()
        {
        }

        public ConfigListenerNotFoundException(string message) : base(message)
        {
        }

        public ConfigListenerNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ConfigListenerNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
