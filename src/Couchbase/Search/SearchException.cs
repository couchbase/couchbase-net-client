using System.Net;

namespace Couchbase.Search
{
    /// <summary>
    /// Represents an error that occured while performing a query operation while using the Search Service.
    /// </summary>
    public class SearchException : CouchbaseException
    {
        /// <summary>
        /// The HTTP status code that was returned by the Search service
        /// </summary>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// The <see cref="SearchStatus"/> returned by the Search service.
        /// </summary>
        public SearchStatus Status { get; internal set; }

        /// <summary>
        /// The error response from the service.
        /// </summary>
        public string Context { get; internal set; }
    }
}
