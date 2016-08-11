using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO
{
    /// <summary>
    /// Thrown when an available buffer cannot be obtained from the <see cref="BufferAllocator"/>.
    /// </summary>
    public sealed class BufferUnavailableException : Exception
    {
        public BufferUnavailableException()
        {
        }

        public BufferUnavailableException(string message, params object[] args)
            : base(string.Format(message, args))
        {
        }

        public BufferUnavailableException(string message)
            : base(message)
        {
        }

        public BufferUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
