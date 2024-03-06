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
        [DoesNotReturn]
        public static void ThrowServiceNotAvailableException(ServiceType serviceType) =>
            throw new ServiceNotAvailableException(serviceType);

        [DoesNotReturn]
        public static void ThrowArgumentException(string message, string paramName) =>
            throw new ArgumentException(message, paramName);

        [DoesNotReturn]
        public static void ThrowArgumentNullException(string paramName) =>
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

        public static void ThrowTimeoutException(IOperation operation, Exception innerException, Core.Logging.TypedRedactor redactor, IErrorContext? context = null)
        {
            var message = $"The {operation.OpCode} operation {operation.Opaque}/{redactor.UserData(operation.Key)} timed out after {operation.Elapsed}. " +
                          $"It was retried {operation.Attempts} times using {operation.RetryStrategy.GetType()}. The KvTimeout is {operation.Timeout}.";

            if (operation.IsSent && !operation.IsReadOnly)
            {
                throw new AmbiguousTimeoutException(message, innerException)
                {
                    Context = context
                };
            }

            throw new UnambiguousTimeoutException(message, innerException)
            {
                Context = context
            };
        }

        public static void ThrowFalseTimeoutException(IOperation operation)
        {
            var errorContext = new KeyValueErrorContext
            {
                DispatchedFrom = operation.LastDispatchedFrom,
                DispatchedTo = operation.LastDispatchedTo,
                DocumentKey = operation.Key,
                ClientContextId = operation.ClientContextId,
                Cas = operation.Cas,
                BucketName = operation.BucketName,
                CollectionName = operation.CName,
                ScopeName = operation.SName,
                OpCode = operation.OpCode,
                RetryReasons = operation.RetryReasons
            };

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
