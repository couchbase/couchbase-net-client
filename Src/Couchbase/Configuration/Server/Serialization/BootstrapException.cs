using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Serialization
{
    public class BootstrapException : Exception
    {
        public BootstrapException()
        {
        }

        public BootstrapException(string message) : base(message)
        {
        }

        public BootstrapException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BootstrapException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
