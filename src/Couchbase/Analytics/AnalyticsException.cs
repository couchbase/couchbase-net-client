using System.Net;

namespace Couchbase.Analytics
{
    /// <summary>
    /// Represents an error that occured while performing a query operation while using the Analytics Service.
    /// </summary>
    public class AnalyticsException : CouchbaseException
    {
        /// <summary>
        /// The HTTP status code that was returned by the Analytics service
        /// </summary>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// The <see cref="AnalyticsStatus"/> returned by the Analytics service.
        /// </summary>
        public AnalyticsStatus Status { get; internal set; }

        /// <summary>
        /// The error response from the service.
        /// </summary>
        public string Context { get; internal set; }
    }
}
