using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.Utils
{
    internal static class ThrowHelper
    {
#if !NETCOREAPP6_0_OR_GREATER

        extension(ArgumentNullException)
        {
            public static void ThrowIfNull([NotNull] object? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            {
                if (value is null)
                {
                    ThrowArgumentNullException(paramName);
                }
            }
        }

#endif

        [DoesNotReturn]
        public static void ThrowServiceNotAvailableException(ServiceType serviceType) =>
            throw new ServiceNotAvailableException(serviceType);

        /// <summary>
        /// HELO did not succeed, so nothing about the connection's feature set is known. Keeping the
        /// connection would mean framing operations for features that were never negotiated.
        /// </summary>
        [DoesNotReturn]
        public static void ThrowHelloFailedException(ResponseStatus status) =>
            throw new ConnectException(
                $"HELO failed with {status}, so no server features could be negotiated for this " +
                "connection. Continuing would frame operations for features the server has not " +
                "agreed to, including the collection ID prefix.");

        /// <summary>
        /// The document key plus its leb128 collection ID prefix exceeds the 250 byte key field.
        /// </summary>
        [DoesNotReturn]
        public static void ThrowKeyTooLongForCollectionIdException(int length) =>
            throw new InvalidArgumentException(
                $"The key is too long: {length} bytes including the collection ID prefix, and the " +
                $"maximum is {OperationHeader.MaxKeyLength}. On a connection that has negotiated " +
                "collections the prefix shares the key field with the document ID, so the document " +
                "ID has to be shorter than the maximum by the size of the prefix.");

        /// <summary>
        /// GET_CID came back successful but carried no usable collection ID.
        /// </summary>
        [DoesNotReturn]
        public static void ThrowCollectionIdNotResolvedException(string scopeName, string collectionName) =>
            throw new CouchbaseException(
                $"The server returned a successful GET_CID for '{scopeName}.{collectionName}' with no " +
                "usable collection ID in the body, so there is nothing to frame operations with.");

        /// <summary>
        /// The connection negotiated collections, so the server will read a leb128 collection ID at
        /// offset 0 of the key, but the operation has no collection ID to write. Sending it would
        /// make the server treat the first byte of the document key as the collection.
        /// </summary>
        /// <remarks>
        /// This is an SDK bug rather than a user error: an operation reached the wire without going
        /// through collection ID resolution. See NCBC-4285 and NCBC-4287.
        /// </remarks>
        [DoesNotReturn]
        public static void ThrowMissingCollectionIdException(OpCode opCode) =>
            throw new CouchbaseException(
                $"The {opCode} operation has no collection ID, but the connection has negotiated " +
                "collections and the server will read one from the start of the key. The operation " +
                "was dispatched without resolving a collection ID.");

        [DoesNotReturn]
        public static void ThrowArgumentException(string message, string paramName) =>
            throw new ArgumentException(message, paramName);

        [DoesNotReturn]
        public static void ThrowArgumentNullException(string? paramName) =>
            throw new ArgumentNullException(paramName);

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException() =>
            throw new ArgumentOutOfRangeException();

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException(string paramName) =>
            throw new ArgumentOutOfRangeException(paramName);

        [DoesNotReturn]
        public static void ThrowInvalidArgumentException(string message) =>
            throw new InvalidArgumentException(message);

        [DoesNotReturn]
        public static void ThrowInvalidEnumArgumentException(string argumentName, int invalidValue, Type enumClass) =>
            throw new InvalidEnumArgumentException(argumentName, invalidValue, enumClass);

        [DoesNotReturn]
        public static void ThrowInvalidIndexException(string message) =>
            throw new InvalidIndexException(message);

        [DoesNotReturn]
        public static void ThrowInvalidOperationException(string message) =>
            throw new InvalidOperationException(message);

        [DoesNotReturn]
        public static void ThrowNotSupportedException(string message) =>
            throw new NotSupportedException(message);

        [DoesNotReturn]
        public static void ThrowObjectDisposedException(string objectName) =>
            throw new ObjectDisposedException(objectName);

        [DoesNotReturn]
        public static void ThrowSocketNotAvailableException(string objectName) =>
            throw new SocketNotAvailableException(objectName);

        [DoesNotReturn]
        public static void ThrowOperationCanceledException() =>
            throw new OperationCanceledException();

        [DoesNotReturn]
        public static void ThrowSendQueueFullException() =>
            throw new SendQueueFullException();

        [DoesNotReturn]
        public static void ThrowNodeUnavailableException(string message) =>
            throw new NodeNotAvailableException(message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T EnsureNotNullForDataStructures<T>(this T? value)
            where T : notnull
        {
            if (value == null)
            {
                ThrowInvalidOperationException("Data structure deserialization returned null.");
            }

            return value;
        }

        [DoesNotReturn]
        public static void ThrowTimeoutException(IOperation operation, Exception innerException, Core.Logging.TypedRedactor redactor, IErrorContext? context = null)
        {
            throw CreateTimeoutException(operation, innerException, redactor, context);
        }

        public static Exception CreateTimeoutException(IOperation operation, Exception innerException, Core.Logging.TypedRedactor redactor, IErrorContext? context = null)
        {
            var message = $"The {operation.OpCode} operation {operation.Opaque}/{redactor.UserData(operation.Key)} timed out after {operation.Elapsed}. " +
                          $"It was retried {operation.Attempts} times using {operation.RetryStrategy.GetType()}. The KvTimeout is {operation.Timeout}.";

            if (operation.IsSent && !operation.IsReadOnly)
            {
                return new AmbiguousTimeoutException(message, innerException)
                {
                    Context = context
                };
            }

            return new UnambiguousTimeoutException(message, innerException)
            {
                Context = context
            };
        }

        [DoesNotReturn]
        public static void ThrowUnsupportedException(string? message)
        {
            throw new UnsupportedException(message);
        }

        [DoesNotReturn]
        public static void ThrowFalseTimeoutException(IOperation operation, KeyValueErrorContext errorContext)
        {
            errorContext.Message = $"The Operation ({operation.OpCode}) was incomplete, and its Lifetime ({operation.Elapsed.TotalSeconds}s) is inferior to its Timeout ({operation.Timeout.TotalSeconds}s) value.";
            throw new CouchbaseException(errorContext);
        }

        [DoesNotReturn]
        public static void ThrowJsonException(string? message = null)
        {
            throw new JsonException(message);
        }
        public static FeatureNotAvailableException ThrowFeatureNotAvailableException(string featureName, string productName)
            => new($"The feature {featureName} is not supported when using {productName}.");

        internal static void ThrowIfIsEnterpriseAnalytics(string? productName)
        {
            if (productName is not null && string.Equals(productName, "analytics"))
            {
                throw new CouchbaseException(
                    "This SDK is for Couchbase Server (operational) clusters, but the remote cluster is an Enterprise Analytics cluster. " +
                    "Please use the Enterprise Analytics SDK to access this cluster");
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
