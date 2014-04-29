using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase
{
    /// <summary>
    /// Thrown when a
    /// </summary>
    public class NullConfigException : Exception
    {
        public NullConfigException()
        {
        }

        public NullConfigException(string message) : base(message)
        {
        }

        public NullConfigException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NullConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
