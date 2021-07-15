using System;

#nullable enable

namespace Couchbase.Core.Exceptions.View
{
    public abstract class ViewException : CouchbaseException<IViewErrorContext>
    {
        protected ViewException() { }

        protected ViewException(IViewErrorContext context) : base(context) { }

        protected ViewException(string message) : base(message) { }

        protected ViewException(string message, Exception? innerException) : base(message, innerException) { }
    }
}
