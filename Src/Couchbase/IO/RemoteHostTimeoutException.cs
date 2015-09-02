using System.IO;

namespace Couchbase.IO
{
    public class RemoteHostTimeoutException : IOException
    {
        public RemoteHostTimeoutException(string message)
            : base(message)
        {
        }
    }
}
