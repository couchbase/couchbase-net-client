namespace Couchbase.Views
{
    public class DesignDocumentNotFoundException : CouchbaseException
    {
        public DesignDocumentNotFoundException(string bucketName, string viewName)
            : base($"Design document does not exist {bucketName}/{viewName}")
        {

        }
    }
}
