namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// Raised when a comparison between a stored document's CAS does not match the CAS provided by the
    /// request indicating the document has been mutated. Each time the document changes its CAS changes.
    /// A form of optimistic concurrency.
    /// </summary>
    public class CasMismatchException : CouchbaseException
    {
        public CasMismatchException()
        {
        }

        public CasMismatchException(IErrorContext context) : base(context.Message)
        {
            Context = context;
        }
    }
}
