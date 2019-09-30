using System;

namespace Couchbase.KeyValue
{
    public class DocumentNotJsonException : KeyValueException
    {
        public DocumentNotJsonException()
        {
        }

        public DocumentNotJsonException(string message)
            : base(message)
        {
        }

        public DocumentNotJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
