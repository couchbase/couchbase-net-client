using System;
using System.Collections.Generic;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using OpenTracing;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Represents a request for a N1QL query
    /// </summary>
    /// <returns></returns>
    public interface IQueryRequest
    {
        /// <summary>
        /// Sets the lifespan of the query request; used to check if the request exceeded the maximum time
        /// configured for it in <see cref="ClientConfiguration.QueryRequestTimeout"/>
        /// </summary>
        /// <value>
        /// The lifespan.
        /// </value>
        Lifespan Lifespan { get; set; }

        /// <summary>
        /// Returns true if the request is not ad-hoc and has been optimized using <see cref="Prepared"/>.
        /// </summary>
        bool IsPrepared { get; }

        /// <summary>
        /// Gets a value indicating whether this query statement is to executed in an ad-hoc manner.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is ad-hoc; otherwise, <c>false</c>.
        /// </value>
        bool IsAdHoc { get; }

        /// <summary>
        /// Gets a value indicating whether this instance has been retried (if it's been optimized
        /// and prepared then the server marked it as stale/not runnable).
        /// </summary>
        /// <value><c>true</c> if this instance has been retried once, otherwise <c>false</c>.</value>
        bool HasBeenRetried { get; set; }


        /// <summary>
        /// Gets a value indicating whether use the <see cref="StreamingQueryClient"/>.
        /// </summary>
        /// <value>
        /// <c>true</c> if [use streaming client]; otherwise, <c>false</c>.
        /// </value>
        bool IsStreaming { get; }

        /// <summary>
        /// Specifies the maximum parallelism for the query. A zero or negative value means the number of logical
        /// cpus will be used as the parallelism for the query. There is also a server wide max_parallelism parameter
        /// which defaults to 1. If a request includes max_parallelism, it will be capped by the server max_parallelism.
        /// If a request does not include max_parallelism, the server wide max_parallelism will be used.
        /// </summary>
        /// <value>
        /// The maximum server parallelism.
        /// </value>
        IQueryRequest MaxServerParallelism(int parallelism);

        /// <summary>
        ///  If set to false, the client will try to perform optimizations
        ///  transparently based on the server capabilities, like preparing the statement and
        ///  then executing a query plan instead of the raw query.
        /// </summary>
        /// <param name="adHoc">if set to <c>false</c> the query will be optimized if possible.</param>
        /// <remarks>The default is <c>true</c>; the query will executed in an ad-hoc manner,
        ///  without special optomizations.</remarks>
        /// <returns></returns>
        IQueryRequest AdHoc(bool adHoc);

        /// <summary>
        ///  Sets a N1QL statement to be executed.
        /// </summary>
        /// <param name="statement">Any valid N1QL statement for a POST request, or a read-only N1QL statement (SELECT, EXPLAIN) for a GET request.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>If both prepared and statement are present and non-empty, an error is returned.</remarks>
        /// <remarks>Required if prepared not provided.</remarks>
        IQueryRequest Statement(string statement);

        /// <summary>
        ///  Sets a N1QL statement to be executed in an optimized way using the given queryPlan.
        /// </summary>
        /// <param name="queryPlan">The <see cref="QueryPlan"/> that was prepared beforehand.</param>
        /// <param name="originalStatement">The original statement (eg. SELECT * FROM default) that the user attempted to optimize</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>Required if statement not provided, will erase a previous call to <see cref="Statement"/>.</remarks>
        IQueryRequest Prepared(QueryPlan queryPlan, string originalStatement);

        /// <summary>
        /// Sets the maximum time to spend on the request.
        /// </summary>
        /// <param name="timeOut">Maximum time to spend on the request</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>Optional - the default is 0ms, which means the request runs for as long as it takes.</remarks>
        /// <remarks>There is also a server wide timeout parameter, and the minimum of that and the request timeout is what gets applied. </remarks>
        IQueryRequest Timeout(TimeSpan timeOut);

        /// <summary>
        /// If a GET request, this will always be true otherwise false.
        /// </summary>
        /// <param name="readOnly">True for get requests.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>Any value set here will be overridden by the type of request sent.</remarks>
        IQueryRequest ReadOnly(bool readOnly);

        /// <summary>
        /// Specifies that metrics should be returned with query results.
        /// </summary>
        /// <param name="includeMetrics">True to return query metrics.</param>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest Metrics(bool includeMetrics);

        /// <summary>
        /// Adds a named parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest AddNamedParameter(string name, object value);

        /// <summary>
        ///  Adds a collection of named parameters to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of <see cref="KeyValuePair{K, V}"/> to be sent.</param>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest AddNamedParameter(params KeyValuePair<string, object>[] parameters);

        /// <summary>
        /// Adds a positional parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="value">The value of the positional parameter.</param>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest AddPositionalParameter(object value);

        /// <summary>
        /// Adds a list of positional parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of positional parameters.</param>
        /// <returns></returns>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest AddPositionalParameter(params object[] parameters);

        /// <summary>
        /// Desired format for the query results.
        /// </summary>
        /// <param name="format">An <see cref="Format"/> enum.</param>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest Format(Format format);

        /// <summary>
        /// Specifies the desired character encoding for the query results.
        /// </summary>
        /// <param name="encoding">An <see cref="Encoding"/> enum.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>Optional.</remarks>
        IQueryRequest Encoding(Encoding encoding);

        /// <summary>
        /// Compression format to use for response data on the wire. Possible values are ZIP, RLE, LZMA, LZO, NONE.
        /// </summary>
        /// <param name="compression"></param>
        /// <remarks>Optional. The default is NONE.</remarks>
        /// <remarks>Values are case-insensitive.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest Compression(Compression compression);

        /// <summary>
        ///  Includes a header for the results schema in the response.
        /// </summary>
        /// <param name="includeSignature">True to include a header for the results schema in the response.</param>
        /// <remarks>The default is true.</remarks>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest Signature(bool includeSignature);

        /// <summary>
        /// Specifies the consistency guarantee/constraint for index scanning.
        /// </summary>
        /// <param name="scanConsistency">Specify the consistency guarantee/constraint for index scanning.</param>
        /// <remarks>Optional.</remarks>
        /// <remarks>The default is <see cref="ScanConsistency"/>.NotBounded.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest ScanConsistency(ScanConsistency scanConsistency);

        /// <summary>
        ///  Specifies the maximum time the client is willing to wait for an index to catch up to the vector timestamp in the request. If an index has to catch up, and the <see cref="ScanWait"/> time is exceed doing so, an error is returned.
        /// </summary>
        /// <param name="scanWait">The maximum time the client is willing to wait for index to catch up to the vector timestamp.</param>
        /// <remarks>Optional.</remarks>
        /// <remarks>Can be supplied with <see cref="ScanConsistency"/> values of RequestPlus, StatementPlus and AtPlus.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest ScanWait(TimeSpan scanWait);

        /// <summary>
        /// Pretty print the output.
        /// </summary>
        /// <param name="pretty">True for the pretty.</param>
        /// <remarks>True by default.</remarks>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest Pretty(bool pretty);

        /// <summary>
        /// Adds a set of credentials to the list of credentials, in the form of user/password
        /// </summary>
        /// <param name="username">The bucket or username.</param>
        /// <param name="password">The password of the bucket.</param>
        /// <param name="isAdmin">True if connecting as an admin.</param>
        /// <remarks>Optional.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest AddCredentials(string username, string password, bool isAdmin);

        /// <summary>
        /// A piece of data supplied by the client that is echoed in the response, if present. N1QL makes no assumptions about the meaning of this data and just logs and echoes it.
        /// </summary>
        /// <param name="contextId"></param>
        /// <remarks>Optional.</remarks>
        /// <remarks> Maximum allowed size is 64 characters. A clientContextID longer than 64 characters is cut off at 64 characters.</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest ClientContextId(string contextId);

        /// <summary>
        /// The base <see cref="Uri"/> used to create the request e.g. http://localhost:8093/query
        /// </summary>
        /// <param name="uri"></param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest BaseUri(Uri uri);

        /// <summary>
        /// Adds a raw query parameter and value to the query.
        /// NOTE: This is uncommited and may change in the future.
        /// </summary>
        /// <param name="name">The paramter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest" /> for method chaining.</returns>
        IQueryRequest RawParameter(string name, object value);

        /// <summary>
        /// Gets the <see cref="Uri"/> for the Query service
        /// </summary>
        /// <returns>The <see cref="Uri"/> for the Query service</returns>
        Uri GetBaseUri();

        /// <summary>
        /// Gets the raw, unprepared N1QL statement.
        /// </summary>
        /// <returns></returns>
        string GetOriginalStatement();

        /// <summary>
        /// Gets the prepared payload for this N1QL statement if IsPrepared() is true,
        /// null otherwise.
        /// </summary>
        /// <returns></returns>
        QueryPlan GetPreparedPayload();

        /// <summary>
        /// Gets a <see cref="IDictionary{K, V}"/> of the name/value pairs to be POSTed to the service if <see cref="Method.Post"/> is used.
        /// </summary>
        /// <returns>The <see cref="IDictionary{K, V}"/> of the name/value pairs to be POSTed to the service.</returns>
        IDictionary<string, object> GetFormValues();

        /// <summary>
        /// Gets the JSON representation of this query for execution in a POST.
        /// </summary>
        /// <returns>The form values as a JSON object.</returns>
        string GetFormValuesAsJson();

        /// <summary>
        /// True if the request exceeded it's <see cref="ClientConfiguration.QueryRequestTimeout"/>
        /// </summary>
        /// <returns></returns>
        bool TimedOut();

        /// <summary>
        /// Gets the context identifier for the N1QL query request/response. Useful for debugging.
        /// </summary>
        /// <remarks>This value changes for every request./></remarks>
        /// <value>
        /// The context identifier.
        /// </value>
        string CurrentContextId { get; }

        /// <summary>
        /// Provides a means of ensuring "read your own wites" or RYOW consistency on the current query.
        /// </summary>
        /// <remarks>Note: <see cref="ScanConsistency"/> will be overwritten to <see cref="N1QL.ScanConsistency.AtPlus"/>.</remarks>
        /// <param name="mutationState">State of the mutation.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest ConsistentWith(MutationState mutationState);

        /// <summary>
        /// Uses the streaming API for the returned results. This is useful for large result sets in that it limits the
        /// working size of the query and helps reduce the possibility of a <see cref="OutOfMemoryException"/> from occurring.
        /// </summary>
        /// <param name="streaming">if set to <c>true</c> streams the results as you iterate through the response.</param>
        /// <returns></returns>
        IQueryRequest UseStreaming(bool streaming);

        /// <summary>
        /// The current active <see cref="ISpan"/> used for tracing.
        /// Intended for internal use only.
        /// </summary>
        ISpan ActiveSpan { get; set; }

        /// <summary>
        /// Indicates if a profile section should be requested in the result. Default is <see cref="QueryProfile.Off"/>.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <returns></returns>
        IQueryRequest Profile(QueryProfile profile);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
