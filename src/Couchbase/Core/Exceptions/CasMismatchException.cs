namespace Couchbase.Core.Exceptions
{
    public class CasMismatchException : CouchbaseException
    {
        public CasMismatchException(IErrorContext context) : base(context.Message)
        {
            Context = context;
        }
    }
}
