namespace Couchbase.Management.Views
{
    public class DesignDocumentExistsException : CouchbaseException
    {
        public DesignDocumentExistsException(string bucketName, string viewName)
            : base($"Design document already exist {bucketName}/{viewName}")
        {

        }
    }
}
