using System;

#nullable enable

namespace Couchbase.Core.Exceptions.KeyValue
{
    public abstract class KeyValueException : CouchbaseException<IKeyValueErrorContext>
    {
        protected KeyValueException() {}

        protected KeyValueException(IErrorContext context) : base(context) {}

        protected KeyValueException(IKeyValueErrorContext context) : base(context) {}

        protected KeyValueException(string message) : base(message) {}

        protected KeyValueException(string message, Exception? innerException) : base(message, innerException) {}
    }
}
