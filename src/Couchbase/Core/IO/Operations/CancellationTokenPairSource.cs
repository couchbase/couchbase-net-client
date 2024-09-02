using System;
using System.Threading;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Represents a pair of cancellation tokens for a K/V operation. One token is the token which
    /// is provided by the SDK consumer to request cancellation. The other is a token which
    /// represents internal cancellation reasons, such as timeouts.
    /// </summary>
    /// <remarks>
    /// It is important to <see cref="Dispose"/> this object in order to avoid memory leaks. Such leaks
    /// may occur if one of the supplied CancellationToken instances is long-lived or if <see cref="FromTimeout"/>
    /// is used to apply a timeout.
    /// </remarks>
    internal sealed class CancellationTokenPairSource : CancellationTokenSource
    {
        private CancellationToken _externalToken;
        private CancellationTokenRegistration _externalRegistration;
        private int _isExternalCancellation;

        /// <summary>
        /// Token which is provided by the SDK consumer to request cancellation.
        /// </summary>
        public CancellationToken ExternalToken
        {
            get => _externalToken;
            set
            {
                // Use a lock to ensure registration cleanup and re-registration are atomic with TryReset.
                lock (this)
                {
                    if (_externalToken != value)
                    {
                        // Cleanup the previous registration, if any
                        _externalRegistration.Dispose();

                        _externalToken = value;
                        _externalRegistration = RegisterExternalCancellationCallback(value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="CancellationTokenPair"/> triggered by this source.
        /// </summary>
        // CancellationTokenPair is a simple struct with one field, so this operation is very inexpensive.
        public CancellationTokenPair TokenPair => new(this);

        /// <summary>
        /// Indicates if the pair has been canceled by the <see cref="ExternalToken"/>.
        /// </summary>
        public bool IsExternalCancellation => Volatile.Read(ref _isExternalCancellation) != 0;

        /// <summary>
        /// Indicates if the pair has been canceled but not the <see cref="ExternalToken"/>.
        /// </summary>
        public bool IsInternalCancellation => IsCancellationRequested && !IsExternalCancellation;

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> which triggered cancellation, or <see cref="CancellationToken.None"/>
        /// if cancellation has not been requested.
        /// </summary>
        public CancellationToken CanceledToken =>
            IsCancellationRequested
                ? IsExternalCancellation ? ExternalToken : Token
                : default;

        public CancellationTokenPairSource() : base()
        {
        }

        /// <summary>
        /// Constructs a CancellationTokenPairSource which will cancel after a set delay.
        /// </summary>
        /// <param name="delay">Delay befor the token is canceled.</param>
        public CancellationTokenPairSource(TimeSpan delay) : base(delay)
        {
        }

        /// <summary>
        /// Constructs a CancellationTokenPairSource which will cancel after a set delay.
        /// </summary>
        /// <param name="delay">Delay befor the token is canceled.</param>
        /// <param name="externalToken">Token which is provided by the SDK consumer to request cancellation.</param>
        public CancellationTokenPairSource(TimeSpan delay, CancellationToken externalToken) : base(delay)
        {
            _externalToken = externalToken;
            _externalRegistration = RegisterExternalCancellationCallback(externalToken);
        }

        /// <summary>
        /// Constructs a CancellationTokenPairSource.
        /// </summary>
        /// <param name="externalToken">Token which is provided by the SDK consumer to request cancellation.</param>
        public CancellationTokenPairSource(CancellationToken externalToken) : base()
        {
            _externalToken = externalToken;
            _externalRegistration = RegisterExternalCancellationCallback(externalToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cleanup below is not necessary if disposing == false because garbage collection is doing cleanup
                _externalRegistration.Dispose();
                _externalRegistration = default;
                _externalToken = default;
            }

            base.Dispose(disposing);
        }

#if NET6_0_OR_GREATER

        /// <summary>
        /// Attempts to reset the <see cref="CancellationTokenPairSource"/> to be used for an unrelated operation.
        /// </summary>
        /// <returns>
        /// true if the <see cref="CancellationTokenPairSource"/> has not had cancellation requested and could
        /// have its state reset to be reused for a subsequent operation; otherwise, false.
        /// </returns>
        public new bool TryReset()
        {
            // Use a lock to ensure registration cleanup is atomic with the ExternalToken setter.
            lock (this)
            {
                // Cleanup the previous registration, if any
                _externalRegistration.Dispose();
                _externalRegistration = default;

                // There's a possible race condition where the current external token has requested
                // cancellation but the callback hasn't fired yet to cancel our token, but the Dispose above
                // didn't complete before this started. This could cause a CTPS returned to a pool to be
                // canceled after it's returned. To protect against this we'll check if the external token
                // is already canceled and if so return false.
                if (_externalToken.IsCancellationRequested)
                {
                    return false;
                }

                _externalToken = default;
            }

            // Now try to reset our CTS
            return base.TryReset();
        }

#endif

        private static void ExternalCancellationCallback(object? state)
        {
            var localThis = (CancellationTokenPairSource)state!;

            if (!localThis.IsCancellationRequested)
            {
                // The CTS is not yet canceled, so we can cancel it now and mark this as an external cancellation.
                // This isn't always done so that a cancellation of the external token after the CTS cancellation
                // has already occurred doesn't cause the cancellation to become ambiguous.
                //
                // There is a slight race condition here if both tokens cancel simultaneously, it could very
                // briefly be an internal cancellation and then get marked as external. The solution is to
                // track a 3-way state of not canceled, internal canceled, and external canceled, but that
                // requires an additional CTS registration that doesn't seem worth the expense since the
                // corner case is very rare and not critical.

                Volatile.Write(ref localThis._isExternalCancellation, 1);
                localThis.Cancel();
            }
        }

        private CancellationTokenRegistration RegisterExternalCancellationCallback(CancellationToken externalToken)
        {
            // Don't do this registration until after initialization is complete because it could
            // trigger an immediate callback if the token is already canceled.

            if (externalToken.CanBeCanceled && !IsCancellationRequested)
            {
#if NETCOREAPP3_1_OR_GREATER
                // UnsafeRegister is prefered when available because we do not need to capture the ExecutionContext.
                var registration = externalToken.UnsafeRegister(ExternalCancellationCallback, this);
#else
                var registration = externalToken.Register(
                    ExternalCancellationCallback, this, useSynchronizationContext: false);
#endif

                // It's possible another thread canceled us while the registration was in progress, double check for
                // this scenario and dispose the registration if it happened.
                if (IsCancellationRequested)
                {
                    registration.Dispose();
                    return default;
                }

                return registration;
            }

            // No registration
            return default;
        }

        /// <summary>
        /// Creates a CancellationTokenPairSource using a single external token.
        /// </summary>
        /// <param name="externalToken">The external token.</param>
        /// <returns>The CancellationTokenPairSource.</returns>
        public static CancellationTokenPairSource FromExternalToken(CancellationToken externalToken) =>
            new(externalToken);

        /// <summary>
        /// Creates a CancellationTokenPairSource with a timeout.
        /// </summary>
        /// <param name="timeout">Timeout to trigger the cancellation token.</param>
        /// <param name="externalToken">Token which is provided by the SDK consumer to request cancellation.</param>
        /// <returns>The CancellationTokenPairSource.</returns>
        public static CancellationTokenPairSource FromTimeout(TimeSpan timeout,
            CancellationToken externalToken = default) =>
            new(timeout, externalToken);
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
