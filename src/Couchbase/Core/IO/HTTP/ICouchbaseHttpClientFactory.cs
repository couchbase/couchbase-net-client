using System;
using System.Net.Http;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// Creates an <see cref="HttpClient"/> which may be safely configured and disposed, but while
    /// reusing inner handlers for connection pooling and HTTP keep-alives.
    /// </summary>
    internal interface ICouchbaseHttpClientFactory
    {
        /// <summary>
        /// Creates an <see cref="HttpClient"/> which may be safely configured and disposed, but while
        /// reusing inner handlers for connection pooling and HTTP keep-alives.
        /// </summary>
        /// <returns>
        /// An <see cref="HttpClient"/> intended to be short-lived.
        /// </returns>
        /// <remarks>
        /// It is safe to dispose this after every use. It reuses the inner HttpMessageHandler.
        /// </remarks>
        HttpClient Create();

        /// <summary>
        /// Default response streaming behavior for HTTP requests. Controlled by <see cref="TuningOptions.StreamHttpResponseBodies"/>.
        /// </summary>
        HttpCompletionOption DefaultCompletionOption { get; }

        /// <summary>
        /// Shared HttpMessageHandler for all HttpClients created by this factory.
        /// This is also used by the <see cref="WebSocketClientHandler"/> to handle secure authentication.
        /// </summary>
        HttpMessageHandler Handler { get; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
