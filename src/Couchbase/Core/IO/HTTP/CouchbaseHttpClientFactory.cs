using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// Default implementation of <see cref="CouchbaseHttpClientFactory"/>.
    /// </summary>
    internal sealed class CouchbaseHttpClientFactory : ICouchbaseHttpClientFactory
    {
        private readonly ClusterContext _context;
        private readonly ILogger<CouchbaseHttpClientFactory> _logger;
        private readonly IRedactor _redactor;
        private readonly ICertificateValidationCallbackFactory _callbackFactory;
        private readonly object _handlerLock = new object();
        internal volatile HttpMessageHandler _sharedHandler;
        private IAuthenticator? _currentAuthenticator; //reference to current authenticator for detecting change

        public CouchbaseHttpClientFactory(ClusterContext context, ILogger<CouchbaseHttpClientFactory> logger, IRedactor redactor, ICertificateValidationCallbackFactory callbackFactory)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (context == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(context));
            }

            if (logger == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }

            if (redactor == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(redactor));
            }

            if (callbackFactory == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(callbackFactory));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _context = context;
            _logger = logger;
            _redactor = redactor;
            _callbackFactory = callbackFactory;

            DefaultCompletionOption = _context.ClusterOptions.Tuning.StreamHttpResponseBodies
                ? HttpCompletionOption.ResponseHeadersRead
                : HttpCompletionOption.ResponseContentRead;

            _sharedHandler = CreateClientHandler();
            _currentAuthenticator = _context.ClusterOptions.GetEffectiveAuthenticator();
        }

        /// <inheritdoc />
        public HttpCompletionOption DefaultCompletionOption { get; }

        public HttpMessageHandler Handler => _sharedHandler;

        /// <inheritdoc />
        public HttpClient Create()
        {
            var authenticator = _context.ClusterOptions.GetEffectiveAuthenticator();

            // Check and potentially recreate handler
            if (ShouldRecreateHandler(authenticator))
            {
                RecreateHandler(authenticator);
            }

            var httpClient = new HttpClient(_sharedHandler, false)
            {
                DefaultRequestHeaders =
                {
                    ExpectContinue = _context.ClusterOptions.EnableExpect100Continue
                }
            };

#if NET5_0_OR_GREATER
            //experimental support for HTTP V.2
            if (_context.ClusterOptions.Experiments.EnableHttpVersion2)
            {
                httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                httpClient.DefaultRequestVersion = HttpVersion.Version20;
            }
#endif

            ClientIdentifier.SetUserAgent(httpClient.DefaultRequestHeaders);

            return httpClient;
        }

        private HttpMessageHandler CreateClientHandler()
        {
            var clusterOptions = _context.ClusterOptions;
            var authenticator = clusterOptions.GetEffectiveAuthenticator();

            if (clusterOptions.IsCapella && !clusterOptions.EffectiveEnableTls)
            {
                _logger.LogWarning("TLS is required when connecting to Couchbase Capella. Please enable TLS by prefixing the connection string with \"couchbases://\" (note the final 's').");
            }

            // Validate authenticator supports current TLS mode
            if (clusterOptions.EffectiveEnableTls && !authenticator.SupportsTls)
            {
                throw new InvalidConfigurationException($"Authenticator {authenticator.GetType().Name} does not support TLS connections");
            }

            if (!clusterOptions.EffectiveEnableTls && !authenticator.SupportsNonTls)
            {
                throw new InvalidConfigurationException($"Authenticator {authenticator.GetType().Name} requires TLS connections");
            }

#if !NETCOREAPP3_1_OR_GREATER
            var handler = new HttpClientHandler();
#else
            var handler = new SocketsHttpHandler();

            if (_context.ClusterOptions.IdleHttpConnectionTimeout > TimeSpan.Zero)
            {
                //https://issues.couchbase.com/browse/MB-37032
                handler.PooledConnectionIdleTimeout = _context.ClusterOptions.IdleHttpConnectionTimeout;
            }

            if (_context.ClusterOptions.HttpConnectionLifetime > TimeSpan.Zero)
            {
                handler.PooledConnectionLifetime = _context.ClusterOptions.HttpConnectionLifetime;
            }

#endif
            authenticator.AuthenticateHttpHandler(handler, clusterOptions, _callbackFactory, _logger);


#if NET5_0_OR_GREATER
            if (_context.ClusterOptions.EnableTcpKeepAlives)
            {
                handler.KeepAlivePingDelay = _context.ClusterOptions.TcpKeepAliveInterval;
                handler.KeepAlivePingTimeout = _context.ClusterOptions.TcpKeepAliveTime;
                handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always;
            }
#endif
            try
            {
                if (_context.ClusterOptions.MaxHttpConnections > 0)
                {
                    //0 means the WinHttpHandler default size of Int.MaxSize is used
                    handler.MaxConnectionsPerServer = _context.ClusterOptions.MaxHttpConnections;
                }
            }
            catch (PlatformNotSupportedException e)
            {
                _logger.LogDebug(e, "Cannot set MaxConnectionsPerServer, not supported on this platform");
            }

            return new AuthenticatingHttpMessageHandler(handler, _context);
        }

        /// <summary>
        /// Determines if the shared handler needs to be recreated due to authenticator/certificate changes.
        /// </summary>
        /// <param name="authenticator">The current authenticator from cluster options.</param>
        /// <returns>True if the handler should be recreated.</returns>
        private bool ShouldRecreateHandler(IAuthenticator authenticator)
        {
            // Only CertificateAuthenticator requires handler recreation
            if (authenticator is not CertificateAuthenticator certAuth)
            {
                return false;
            }

            // Case 1: Authenticator changed to a different instance via cluster.Authenticator()
            if (!ReferenceEquals(authenticator, _currentAuthenticator))
            {
                return true;
            }

            // Case 2: Same CertificateAuthenticator but with a rotating certificate factory that has updates
            if (certAuth.CertificateFactory is IRotatingCertificateFactory { HasUpdates: true })
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Re-creates the Shared HttpHandler
        /// </summary>
        /// <param name="authenticator">The current authenticator from cluster options.</param>
        private void RecreateHandler(IAuthenticator authenticator)
        {
            lock (_handlerLock)
            {
                // Check again in case concurrent threads passed the first check,
                // such that they immediately exit after acquiring the lock
                // instead of pursuing to re-create the handler
                if (!ShouldRecreateHandler(authenticator))
                {
                    return;
                }

                // Create new handler before disposing the old one
                var oldHandler = _sharedHandler;

                _sharedHandler = CreateClientHandler();
                _currentAuthenticator = authenticator;

                // Dispose old handler. This may pull the rug on in-flight requests
                oldHandler.Dispose();
            }
        }
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
