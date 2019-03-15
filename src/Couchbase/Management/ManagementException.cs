using System.Net;

namespace Couchbase.Management
{
    /// <summary>
    /// Represents an error that occured while performing an operation while using a Management API.
    /// </summary>
    public class ManagementException : CouchbaseException
    {
        /// <summary>
        /// The HTTP status code that was returned by the Search service
        /// </summary>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// The error response from the service.
        /// </summary>
        public string Context { get; internal set; }
    }
}
