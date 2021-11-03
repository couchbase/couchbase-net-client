using System;
using System.Runtime.InteropServices;
using System.Threading;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Represents a pair of cancellation tokens for a K/V operation. One token is the token which
    /// is provided by the SDK consumer to request cancellation. The other is a token which
    /// represents internal cancellation reasons, such as timeouts.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct CancellationTokenPair
    {
        private readonly CancellationTokenPairSource? _source;

        /// <inheritdoc cref="CancellationTokenPairSource.ExternalToken"/>
        public CancellationToken ExternalToken => _source?.ExternalToken ?? default;

        /// <inheritdoc cref="CancellationTokenPairSource.InternalToken"/>
        public CancellationToken InternalToken => _source?.InternalToken ?? default;

        /// <inheritdoc cref="CancellationTokenPairSource.GlobalToken"/>
        public CancellationToken GlobalToken => _source?.GlobalToken ?? default;

        /// <inheritdoc cref="CancellationToken.CanBeCanceled" />
        public bool CanBeCanceled => _source?.CanBeCanceled ?? false;

        /// <inheritdoc cref="CancellationToken.IsCancellationRequested" />
        public bool IsCancellationRequested => _source?.IsCancellationRequested ?? false;

        /// <inheritdoc cref="CancellationTokenPairSource.IsExternalCancellation"/>
        public bool IsExternalCancellation => _source?.IsExternalCancellation ?? false;

        /// <inheritdoc cref="CancellationTokenPairSource.IsInternalCancellation"/>
        public bool IsInternalCancellation => _source?.IsInternalCancellation ?? false;

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> which triggered cancellation, or <see cref="CancellationToken.None"/>
        /// if cancellation has not been requested.
        /// </summary>
        public CancellationToken CanceledToken => _source?.CanceledToken ?? default;

        /// <summary>
        /// Constructs a CancellationTokenPair.
        /// </summary>
        /// <param name="source">The source of the cancellation token pair.</param>
        /// <remarks>
        /// Should be used by <see cref="CancellationTokenPairSource"/> only, do not call directly.
        /// </remarks>
        public CancellationTokenPair(CancellationTokenPairSource source)
        {
            _source = source;
        }

        /// <inheritdoc cref="CancellationToken.ThrowIfCancellationRequested" />
        public void ThrowIfCancellationRequested() => _source?.ThrowIfCancellationRequested();

        /// <inheritdoc cref="CancellationToken.Register(Action)" />
        public CancellationTokenRegistration Register(Action callback) =>
            _source?.Register(callback) ?? default;

        /// <inheritdoc cref="CancellationToken.Register(Action{object}, object)" />
        public CancellationTokenRegistration Register(Action<object?> callback, object? state) =>
            _source?.Register(callback, state) ?? default;

#if NETCOREAPP3_1_OR_GREATER

        /// <inheritdoc cref="CancellationToken.UnsafeRegister(Action{object}, object)" />
        public CancellationTokenRegistration UnsafeRegister(Action<object?> callback, object? state) =>
            _source?.UnsafeRegister(callback, state) ?? default;

#endif

        public static implicit operator CancellationToken(CancellationTokenPair cancellationTokenPair) =>
            cancellationTokenPair.GlobalToken;
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
