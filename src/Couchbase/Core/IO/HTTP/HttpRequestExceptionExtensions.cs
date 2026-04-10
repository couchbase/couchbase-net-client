using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// Extension methods for classifying <see cref="HttpRequestException"/> transport errors
    /// to determine whether an HTTP service request is safe to retry against another node.
    /// </summary>
    internal static class HttpRequestExceptionExtensions
    {
        /// <summary>
        /// Socket error codes that definitively indicate the request never reached the
        /// server. Safe to retry even for non-idempotent (mutation) requests.
        /// </summary>
        private static readonly HashSet<SocketError> PreConnectErrors =
        [
            SocketError.ConnectionRefused,    // TCP SYN was RST'd — no service listening
            SocketError.HostNotFound,         // DNS resolution failed
            SocketError.HostUnreachable,      // ICMP host unreachable before connect
            SocketError.NetworkUnreachable,   // No route to host
            SocketError.AddressNotAvailable,  // Local address binding failed
            SocketError.NetworkDown,          // Local network interface is down
        ];

        /// <summary>
        /// Determines if an <see cref="HttpRequestException"/> represents a transport failure
        /// that is safe to retry, considering request idempotency.
        /// </summary>
        /// <param name="ex">The HTTP request exception.</param>
        /// <param name="isReadOnly">Whether the request is read-only (idempotent).</param>
        /// <returns><c>true</c> if the request should be retried; <c>false</c> otherwise.</returns>
        public static bool IsRetriableTransportError(this HttpRequestException ex, bool isReadOnly)
        {
#if NET8_0_OR_GREATER
            var httpRequestError = ex.HttpRequestError;
            if (httpRequestError == HttpRequestError.NameResolutionError ||
                httpRequestError == HttpRequestError.ConnectionError ||
                httpRequestError == HttpRequestError.SecureConnectionError)
            {
                // Definitively pre-connect (including TLS handshake failures) — safe to retry
                return true;
            }
#endif

            var socketError = ex.GetSocketError();
            if (socketError.HasValue && PreConnectErrors.Contains(socketError.Value))
            {
                // Definitively pre-connect — safe to retry regardless of idempotency
                return true;
            }

            // Ambiguous or unknown transport error — only safe if idempotent
            return isReadOnly;
        }

        /// <summary>
        /// If the exception is a retriable transport error, records <paramref name="failingNode"/> on the
        /// request's excluded-nodes list so the next orchestrator attempt selects a different node.
        /// </summary>
        /// <param name="ex">The HTTP request exception.</param>
        /// <param name="isReadOnly">Whether the request is read-only (idempotent).</param>
        /// <param name="request">The request being retried, or <c>null</c> if none is shared with the orchestrator.</param>
        /// <param name="failingNode">The node URI that just failed with a transport error.</param>
        /// <returns><c>true</c> if the caller should return a retriable result; <c>false</c> to rethrow.</returns>
        public static bool TryExcludeFailedNode(this HttpRequestException ex, bool isReadOnly,
            RequestBase? request, Uri failingNode)
        {
            if (!ex.IsRetriableTransportError(isReadOnly))
            {
                return false;
            }

            if (request is not null)
            {
                request.ExcludedNodes ??= new List<Uri>();
                request.ExcludedNodes.Add(failingNode);
            }

            return true;
        }

        /// <summary>
        /// Walks the inner exception chain to find a <see cref="SocketException"/> and return its
        /// <see cref="SocketException.SocketErrorCode"/>. The chain is walked (rather than checking
        /// the immediate <see cref="Exception.InnerException"/>) so the true root cause is found even
        /// when it is nested several levels deep, which is common for <see cref="HttpRequestException"/>.
        /// </summary>
        private static SocketError? GetSocketError(this HttpRequestException ex)
        {
            var inner = ex.InnerException;
            while (inner is not null)
            {
                if (inner is SocketException se)
                    return se.SocketErrorCode;
                inner = inner.InnerException;
            }
            return null;
        }
    }
}
