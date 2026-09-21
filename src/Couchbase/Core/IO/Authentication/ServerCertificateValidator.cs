using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Decides whether a server certificate is trusted. Installed as the
    /// <see cref="RemoteCertificateValidationCallback"/> on KV and HTTP connections
    /// when the user has not supplied a callback of their own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// .NET validates the chain against the OS trust store before invoking the callback and that
    /// verdict is taken as is. When it fails only because the chain does not reach an OS trusted root,
    /// the chain is rebuilt against the configured trust anchors, which are either the user supplied
    /// certificates or the bundled Capella CA. The rebuild uses a chain owned by this class, so the
    /// chain SslStream hands in is never modified.
    /// </para>
    /// <para>
    /// The host name is checked twice, once through the platform verdict and once here. Windows does not
    /// report a name mismatch through <see cref="SslPolicyErrors"/> when the chain is untrusted as well,
    /// which is the normal case for a private or Capella CA, so the platform verdict alone lets a
    /// certificate issued for another host through. The second check needs X509Certificate2.MatchesHostname,
    /// which is .NET 7 and later, so the netstandard builds of this SDK still rely on the platform verdict
    /// alone and remain exposed on Windows. Those users should add the cluster CA to the Windows trust store.
    /// The chain then builds cleanly, no untrusted root error masks the name check, and Windows reports the
    /// mismatch as it should.
    /// </para>
    /// </remarks>
    internal sealed class ServerCertificateValidator
    {
        private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

        private readonly X509Certificate2Collection _trustedCertificates;
        private readonly bool _ignoreNameMismatch;
        private readonly X509RevocationMode _revocationMode;
        private readonly ILogger _logger;
        private readonly IRedactor _redactor;

        public ServerCertificateValidator(
            X509Certificate2Collection trustedCertificates,
            bool ignoreNameMismatch,
            X509RevocationMode revocationMode,
            ILogger logger,
            IRedactor redactor)
        {
            _trustedCertificates = trustedCertificates ?? throw new ArgumentNullException(nameof(trustedCertificates));
            _ignoreNameMismatch = ignoreNameMismatch;
            _revocationMode = revocationMode;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        public bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate is null || chain is null)
            {
                _logger.LogInformation("X509 server presented no certificate ({SslPolicyErrors})", sslPolicyErrors);
                return false;
            }

            var errors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateNameMismatch;
            var reportedNameMismatch = errors != sslPolicyErrors;

            if (_ignoreNameMismatch)
            {
                if (reportedNameMismatch)
                {
                    _logger.LogDebug("X509 ignoring certificate name mismatch");
                }
            }
            else if (reportedNameMismatch || !MatchesTargetHost(sender, certificate))
            {
                _logger.LogInformation("X509 certificate name does not match the target host, rejecting");
                return false;
            }

            if (errors == SslPolicyErrors.None)
            {
                _logger.LogDebug("X509 certificate accepted by the OS trust store");
                return true;
            }

            if (errors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                _logger.LogInformation("X509 certificate rejected ({SslPolicyErrors})", errors);
                return false;
            }

            return ChainsToTrustedCertificate(certificate, chain);
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Checks the certificate against the host name the connection asked for, independently of the
        /// platform verdict. Returns true when there is no host name to check against.
        /// </summary>
        private bool MatchesTargetHost(object sender, X509Certificate certificate)
        {
            if (sender is not SslStream sslStream
                || string.IsNullOrEmpty(sslStream.TargetHostName)
                || certificate is not X509Certificate2 leaf)
            {
                return true;
            }

            try
            {
                return leaf.MatchesHostname(sslStream.TargetHostName);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                // An unparseable host name or a malformed subject alternative name cannot be verified,
                // so the certificate is not trusted for this connection.
                _logger.LogInformation(ex, "X509 certificate name could not be checked against the target host");
                return false;
            }
        }
#else
        private bool MatchesTargetHost(object sender, X509Certificate certificate) => true;
#endif

#if !NET5_0_OR_GREATER
        private bool ChainsToTrustedCertificate(X509Certificate certificate, X509Chain chain)
        {
            _logger.LogWarning(
                "X509 certificate is not trusted by the OS. Custom trust anchors need .NET 5 or later, add the CA to the OS trust store instead.");
            return false;
        }
#else
        private bool ChainsToTrustedCertificate(X509Certificate certificate, X509Chain chain)
        {
            // Everything placed in the stores of this chain is a copy owned here, so nothing the caller or
            // the runtime holds can be disposed by this method.
            var ownedCertificates = new List<X509Certificate2>();
            using var ownChain = new X509Chain();
            try
            {
                var policy = ownChain.ChainPolicy;
                policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                policy.RevocationMode = _revocationMode;
                policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                policy.ApplicationPolicy.Add(new Oid(ServerAuthOid));

                foreach (var trusted in _trustedCertificates)
                {
                    policy.CustomTrustStore.Add(Copy(trusted, ownedCertificates));
                }

                // SslStream places the intermediates the server sent in the ExtraStore of the chain it
                // built. They are needed to reach the anchor but are never trusted on their own.
                foreach (var presented in chain.ChainPolicy.ExtraStore)
                {
                    policy.ExtraStore.Add(Copy(presented, ownedCertificates));
                }

                var leaf = certificate as X509Certificate2 ?? Copy(certificate, ownedCertificates);
                var isTrusted = ownChain.Build(leaf);

                LogOutcome(isTrusted, leaf, ownChain);
                return isTrusted;
            }
            finally
            {
                foreach (var element in ownChain.ChainElements)
                {
                    element.Certificate.Dispose();
                }

                foreach (var owned in ownedCertificates)
                {
                    owned.Dispose();
                }
            }
        }

        private static X509Certificate2 Copy(X509Certificate certificate, List<X509Certificate2> owned)
        {
            var copy = new X509Certificate2(certificate);
            owned.Add(copy);
            return copy;
        }

        private void LogOutcome(bool trusted, X509Certificate2 leaf, X509Chain chain)
        {
            if (trusted)
            {
                _logger.LogDebug("X509 certificate {Subject} accepted against {Count} configured trust anchor(s)",
                    _redactor.SystemData(leaf.Subject), _trustedCertificates.Count);
                return;
            }

            var statuses = string.Join(", ", chain.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation.Trim()}"));
            _logger.LogInformation(
                "X509 certificate {Subject} rejected against {Count} configured trust anchor(s). Chain status: {Status}",
                _redactor.SystemData(leaf.Subject), _trustedCertificates.Count, statuses);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                foreach (var element in chain.ChainElements)
                {
                    _logger.LogTrace("X509 chain element {Certificate}", _redactor.SystemData(element.Certificate));
                }
            }
        }
#endif
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
#endregion
