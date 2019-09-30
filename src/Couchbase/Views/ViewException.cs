using System.Collections.Generic;
using System.Net;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents an error that occured while performing a query operation while using the View Service.
    /// </summary>
    public class ViewException : CouchbaseException
    {
        /// <summary>
        /// The HTTP status code that was returned by the Analytics service
        /// </summary>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// The error response from the service.
        /// </summary>
        public List<string> Context { get; internal set; }
    }
}
