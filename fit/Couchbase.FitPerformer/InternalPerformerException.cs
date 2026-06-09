using System;

namespace Couchbase.FitPerformer
{
    public class InternalPerformerException : Exception
    {
        public InternalPerformerException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}