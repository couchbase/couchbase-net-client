using System;
using System.Threading;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Upon construction, registers with a <see cref="CancellationTokenPair"/> to set the operation to canceled
    /// while following logic rules regarding external vs internal cancellation. Disposing will release the
    /// registration and prevent future cancellation of the operation.
    /// </summary>
    internal struct OperationCancellationRegistration : IDisposable
    {
        private static readonly Action<object?> HandleExternalCancellationAction = HandleExternalCancellation;
        private static readonly Action<object?> HandleInternalCancellationAction = HandleInternalCancellation;

        private CancellationTokenRegistration _internalRegistration;
        private CancellationTokenRegistration _externalRegistration;

        /// <summary>
        /// Creates a new OperationCancellationRegistration.
        /// </summary>
        /// <param name="operation">Operation to be cancelled.</param>
        /// <param name="tokenPair">Token pair which will cancel the operation.</param>
        /// <remarks>
        /// Only one <see cref="OperationCancellationRegistration"/> should be registered at a time on a given operation,
        /// the provided <see cref="CancellationTokenPair"/> is saved for tracking on <see cref="IOperation.TokenPair"/>.
        /// </remarks>
        public OperationCancellationRegistration(IOperation operation, CancellationTokenPair tokenPair)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (operation == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(operation));
            }

            operation.TokenPair = tokenPair;

            // Since we're using static actions, register calls below are a fast noop for tokens which cannot be canceled
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_1
            _externalRegistration = tokenPair.ExternalToken.Register(HandleExternalCancellationAction, operation);
            _internalRegistration = tokenPair.InternalToken.Register(HandleInternalCancellationAction, operation);
#else
            // On .NET Core 3 and later we can further optimize by not flowing the ExecutionContext using UnsafeRegister
            _externalRegistration = tokenPair.ExternalToken.UnsafeRegister(HandleExternalCancellationAction, operation);
            _internalRegistration = tokenPair.InternalToken.UnsafeRegister(HandleInternalCancellationAction, operation);
#endif
        }

        public void Dispose()
        {
            // CancellationTokenRegistration is a value type and Dispose is a noop for the default value,
            // so this is safe in the case where there is no registration.
            _internalRegistration.Dispose();
            _externalRegistration.Dispose();
        }

        /// <summary>
        /// Static method for processing external cancellation. By using a static Action instance and passing the
        /// operation as state we reduce heap allocations.
        /// </summary>
        private static void HandleExternalCancellation(object? state)
        {
            var operation = (IOperation) state!;

            // We should not cancel if the operation is:
            // 1. Currently in flight
            // 2. And a mutation operation
            // In those cases, we keep waiting for the response to avoid ambiguity on mutations

            if (!operation.IsSent || operation.IsReadOnly)
            {
                operation.TrySetCanceled(operation.TokenPair.ExternalToken);
            }
        }

        /// <summary>
        /// Static method for processing internal cancellation. By using a static Action instance and passing the
        /// operation as state we reduce heap allocations.
        /// </summary>
        private static void HandleInternalCancellation(object? state)
        {
            var operation = (IOperation) state!;

            operation.TrySetCanceled(operation.TokenPair.InternalToken);
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
