﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;

namespace Couchbase
{
    /// <summary>
    /// Defines an http client used by the <see cref="T:CouchbaseNode"/> to retrieve data from the views.
    /// </summary>
    public interface IHttpClient
    {
        IHttpRequest CreateRequest(string path);

        int RetryCount { get; set; }
    }

    /// <summary>
    /// represents an http response sent by the Couchbase servers.
    /// </summary>
    public interface IHttpResponse
    {
        Stream GetResponseStream();

        HttpStatusCode StatusCode { get; }
    }

    /// <summary>
    /// Represents an http request sent to the Couchbase servers.
    /// </summary>
    public interface IHttpRequest
    {
        /// <summary>
        /// Adds a parameter to the request. Parameters will be appended to the query string or to the POST body, depending on the type of the request.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void AddParameter(string name, string value);

        /// <summary>
        /// Executes the request and returns the response sent by the server.
        /// </summary>
        /// <returns>The response sent by the server.</returns>
        IHttpResponse GetResponse();

        /// <summary>
        /// The HTTP method of the request.
        /// </summary>
        HttpMethod Method { get; set; }
    }

    /// <summary>
    /// defines a class which is responsible for creating <see cref="IHttpClient"/> instances.
    /// </summary>
    public interface IHttpClientFactory
    {
        IHttpClient Create(Uri baseUri, string username, string password, TimeSpan timeout, bool shouldInitializeConnection);
    }

    public enum HttpMethod
    {
        Get = 0,
        Post = 1,
        Delete = 2,
        Put = 3,
        Head = 4,
        Options = 5
    }
}