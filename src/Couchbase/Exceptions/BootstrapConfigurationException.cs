using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Couchbase.Exceptions
{
    public class BootstrapConfigurationException : Exception
    {
        public BootstrapConfigurationException(string message)
			: base(message)
		{
		}

		public BootstrapConfigurationException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

        public BootstrapConfigurationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
    }
}
