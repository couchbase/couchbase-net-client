using System;
using System.Runtime.InteropServices;
using System.Threading;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Upon construction, registers with a <see cref="CancellationTokenPair"/> to set the operation to canceled.
    /// Disposing will release the registration and prevent future cancellation of the operation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct OperationCancellationRegistration : IDisposable
    {
        private readonly CancellationTokenRegistration _registration;

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
            // useSynchronizationContext: false is the equivalent of awaiting ConfigureAwait(false) for the cancellation callback,
            // it allows the callback to run without synchronizing back to the original context.
            _registration = tokenPair.Register(HandleCancellation, operation, useSynchronizationContext: false);
#else
            // On .NET Core 3 and later we can further optimize by not flowing the ExecutionContext using UnsafeRegister
            _registration = tokenPair.UnsafeRegister(HandleCancellation, operation);
#endif
        }

        public void Dispose()
        {
            // CancellationTokenRegistration is a value type and Dispose is a noop for the default value,
            // so this is safe in the case where there is no registration.
            _registration.Dispose();
        }

        /// <summary>
        /// Static method for processing cancellation. By using a static method and passing the
        /// operation as state we reduce heap allocations.
        /// </summary>
        private static void HandleCancellation(object? state)
        {
            var operation = (IOperation) state!;

            operation.TrySetCanceled(operation.TokenPair.CanceledToken);
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
