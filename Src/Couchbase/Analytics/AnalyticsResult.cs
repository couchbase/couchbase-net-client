using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.N1QL;

namespace Couchbase.Analytics
{
    internal class AnalyticsResult<T> : IAnalyticsResult<T>
    {
        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        /// <value>
        /// The HTTP status code.
        /// </value>
        public HttpStatusCode HttpStatusCode { get; set; }

        /// <summary>
        /// Gets A unique identifier for the response.
        /// </summary>
        /// <value>
        /// The unique identifier for the response.
        /// </value>
        public Guid RequestId { get; set; }

        /// <summary>
        /// Gets the clientContextID of the request, if one was supplied. Used for debugging.
        /// </summary>
        /// <value>
        /// The client context identifier.
        /// </value>
        public string ClientContextId { get; set; }

        /// <summary>
        /// Gets a list of all the objects returned by the query. An object can be any JSON value.
        /// </summary>
        /// <value>
        /// A a list of all the objects returned by the query.
        /// </value>
        public List<T> Rows { get; set; }

        /// <summary>
        /// Gets the status of the request; possible values are: success, running, errors, completed, stopped, timeout, fatal.
        /// </summary>
        /// <value>
        /// The status of the request.
        /// </value>
        public QueryStatus Status { get; set; }

        /// <summary>
        /// Gets the schema of the results. Present only when the query completes successfully.
        /// </summary>
        /// <value>
        /// The signature of the schema of the request.
        /// </value>
        public dynamic Signature { get; set; }

        /// <summary>
        /// Returns true if the operation was succesful.
        /// </summary>
        /// <remarks>
        /// If Success is false, use the Message property to help determine the reason.
        /// </remarks>
        public bool Success { get; set; }

        /// <summary>
        /// If the operation wasn't succesful, a message indicating why it was not succesful.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        public bool ShouldRetry()
        {
            return false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the analytics rows from the result array.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the analytics rows.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the analytics rows from the result array.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the analytics rows.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            return Rows.GetEnumerator();
        }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        public List<Error> Errors { get; set; }

        /// <summary>
        /// Gets a list of 0 or more warning objects; if a warning occurred during processing of the request, it will be represented by a warning object in this list.
        /// </summary>
        /// <value>
        /// The warnings.
        /// </value>
        public List<Warning> Warnings { get; set; }

        /// <summary>
        /// Gets an object containing metrics about the request.
        /// </summary>
        /// <value>
        /// The metrics.
        /// </value>
        public Metrics Metrics { get; set; }
    }

    internal class AnalyticsResultData<T>
    {
        public Guid requestID { get; set; }
        public string clientContextID { get; set; }
        public dynamic signature { get; set; }
        public IEnumerable<T> results { get; set; }
        public QueryStatus status { get; set; }
        public IEnumerable<ErrorData> errors { get; set; }
        public IEnumerable<WarningData> warnings { get; set; }
        public MetricsData metrics { get; set; }

        public AnalyticsResultData()
        {
            results = new List<T>();
            errors = new List<ErrorData>();
            warnings = new List<WarningData>();
            metrics = new MetricsData();
        }

        internal AnalyticsResult<T> ToQueryResult()
        {
            return new AnalyticsResult<T>
            {
                RequestId = requestID,
                ClientContextId = clientContextID,
                Signature = signature,
                Rows = results.ToList(),
                Status = status,
                Errors = errors != null ? errors.Select(e => e.ToError()).ToList() : null,
                Warnings = warnings != null ? warnings.Select(w => w.ToWarning()).ToList() : null,
                Metrics = metrics != null ? metrics.ToMetrics() : null,
            };
        }
    }
}
