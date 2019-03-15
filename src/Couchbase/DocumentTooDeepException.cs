using System;

namespace Couchbase
{
    public class DocumentTooDeepException : KeyValueException
    {
        public DocumentTooDeepException()
        {
        }

        public DocumentTooDeepException(string message)
            : base(message)
        {
        }

        public DocumentTooDeepException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
