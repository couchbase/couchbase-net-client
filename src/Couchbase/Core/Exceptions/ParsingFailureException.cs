namespace Couchbase.Core.Exceptions
{
    public class ParsingFailureException : CouchbaseException
    {
        public ParsingFailureException() { }

        public ParsingFailureException(IErrorContext context)
        {
            Context = context;
        }
    }
}
