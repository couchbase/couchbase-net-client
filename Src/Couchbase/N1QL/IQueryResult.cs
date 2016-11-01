using System;
using System.Collections.Generic;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Interface for the results of a N1QL query.
    /// </summary>
    public interface IQueryResult : IResult, IDisposable
    {
        /// <summary>
        /// Gets A unique identifier for the response.
        /// </summary>
        /// <value>
        /// The unique identifier for the response.
        /// </value>
        Guid RequestId { get; }

        /// <summary>
        /// Gets the clientContextID of the request, if one was supplied. Used for debugging.
        /// </summary>
        /// <value>
        /// The client context identifier.
        /// </value>
        string ClientContextId { get; }

        /// <summary>
        /// Gets the schema of the results. Present only when the query completes successfully.
        /// </summary>
        /// <value>
        /// The signature of the schema of the request.
        /// </value>
        dynamic Signature { get; }

        /// <summary>
        /// Gets the status of the request; possible values are: success, running, errors, completed, stopped, timeout, fatal.
        /// </summary>
        /// <value>
        /// The status of the request.
        /// </value>
        QueryStatus Status { get; }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        List<Error> Errors { get; }

        /// <summary>
        /// Gets a list of 0 or more warning objects; if a warning occurred during processing of the request, it will be represented by a warning object in this list.
        /// </summary>
        /// <value>
        /// The warnings.
        /// </value>
        List<Warning> Warnings { get; }

        /// <summary>
        /// Gets an object containing metrics about the request.
        /// </summary>
        /// <value>
        /// The metrics.
        /// </value>
        Metrics Metrics { get; }
    }
}