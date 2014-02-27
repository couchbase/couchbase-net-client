using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core
{
    public class BucketAlreadyOpenException : Exception
    {
        public BucketAlreadyOpenException()
        {
        }

        public BucketAlreadyOpenException(string message) : base(message)
        {
        }

        protected BucketAlreadyOpenException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public BucketAlreadyOpenException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
