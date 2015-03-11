using System;
using System.Collections.Generic;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Represents a request for a N1QL query
    /// </summary>
    /// <returns></returns>
    public interface IQueryRequest
    {
        /// <summary>
        /// The HTTP method type to use.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        bool IsPost { get; }

        /// <summary>
        /// Returns true if the request is a prepared statement
        /// </summary>
        bool IsPrepared { get; }

        /// <summary>
        /// The HTTP method type to use.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        IQueryRequest HttpMethod(Method method);

        /// <summary>
        ///  Sets a N1QL statement to be executed.
        /// </summary>
        /// <param name="statement">Any valid N1QL statement for a POST request, or a read-only N1QL statement (SELECT, EXPLAIN) for a GET request.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>If both prepared and statement are present and non-empty, an error is returned.</remarks>
        /// <remarks>Required if prepared not provided.</remarks>
        IQueryRequest Statement(string statement);

        /// <summary>
        ///  Sets a N1QL statement to be executed.
        /// </summary>
        /// <param name="queryPlan">The <see cref="IQueryPlan"/> that was prepared beforehand.</param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>If both prepared and statement are present and non-empty, an error is returned.</remarks>
        /// <remarks>Required if statement not provided.</remarks>
        IQueryRequest Prepared(IQueryPlan queryPlan);



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
        /// Specify the lower bound vector timestamp when using at_plus scan consistency.
        /// </summary>
        /// <param name="vector"></param>
        ///<remarks>Required if <see cref="ScanConsistency"/> is AtPlus.</remarks>
        ///<remarks>There are two formats: array of 1024 numbers, to specify a full scan vector and object containing vbucket/seqno pairs, to specify a sparse scan vector (e.g. { "5 ": 5409393,  "19": 47574574 })</remarks>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        IQueryRequest ScanVector(dynamic vector);

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
        /// Gets the constructed <see cref="Uri"/> for making the request.
        /// </summary>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        Uri GetRequestUri();

        /// <summary>
        /// Gets the <see cref="Uri"/> for the Query service
        /// </summary>
        /// <returns>The <see cref="Uri"/> for the Query service</returns>
        Uri GetBaseUri();

        string GetStatement();

        /// <summary>
        /// Gets a <see cref="IDictionary{K, V}"/> of the name/value pairs to be POSTed to the service if <see cref="Method.Post"/> is used.
        /// </summary>
        /// <returns>The <see cref="IDictionary{K, V}"/> of the name/value pairs to be POSTed to the service.</returns>
        IDictionary<string, string> GetFormValues();

        string GetQueryParameters();
    }
}
