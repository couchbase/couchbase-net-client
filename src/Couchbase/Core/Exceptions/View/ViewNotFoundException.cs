using System;

namespace Couchbase.Core.Exceptions.View
{
    public class ViewNotFoundException : CouchbaseException
    {
        public ViewNotFoundException(ViewContextError context)
        {
            Context = context;
        }

        public ViewNotFoundException() { }

        public ViewNotFoundException(string message) : base(message) { }

        public ViewNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
