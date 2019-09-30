namespace Couchbase.Views
{
    public class DesignDocumentNotFound : CouchbaseException
    {
        public DesignDocumentNotFound(string bucketName, string designDocumentName)
            : base($"Design document does not exist {bucketName}/{designDocumentName}")
        {

        }
    }
}