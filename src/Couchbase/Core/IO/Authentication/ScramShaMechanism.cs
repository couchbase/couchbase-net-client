using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Provides a SCRAM-SHA authentication implementation for Couchbase Server.
    /// Supports SHA-256 and SHA-512 on .NET 8+, and SHA-1 on netstandard2.x targets.
    /// </summary>
    /// <seealso cref="ISaslMechanism" />
    internal class ScramShaMechanism : SaslMechanismBase
    {
        private const string ClientKey = "Client Key";

        /// <summary>
        /// Client-supported SCRAM mechanisms in strongest-first preference order, constrained by the
        /// target framework. On .NET 8+, SHA-256/512 are available via <c>Rfc2898DeriveBytes.Pbkdf2</c>
        /// and SHA-1 is disallowed for new HMAC/PBKDF2 use (NIST SP 800-131A Rev 2). On netstandard2.x,
        /// <see cref="Rfc2898DeriveBytes"/> only supports SHA-1 PBKDF2, so SHA-1 remains the only option.
        /// </summary>
#if NET8_0_OR_GREATER
        internal static readonly MechanismType[] ClientSupportedMechanisms =
            { MechanismType.ScramSha512, MechanismType.ScramSha256 };
#else
#pragma warning disable CS0618 // ScramSha1 is obsolete but is the only supported mechanism on netstandard
        internal static readonly MechanismType[] ClientSupportedMechanisms =
            { MechanismType.ScramSha1 };
#pragma warning restore CS0618
#endif

        /// <summary>
        /// Output byte length for PBKDF2 and HMAC, determined by the negotiated hash algorithm:
        /// SHA-1 → 20 bytes, SHA-256 → 32 bytes, SHA-512 → 64 bytes.
        /// </summary>
        private int ShaByteLength => MechanismType switch
        {
            MechanismType.ScramSha512 => 64,
            MechanismType.ScramSha256 => 32,
            _ => 20
        };

        private readonly string _username;
        private readonly string _password;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScramShaMechanism"/> class.
        /// </summary>
        /// <param name="mechanismType">Type of the mechanism.</param>
        /// <param name="password">The password for the user.</param>
        /// <param name="username">The user's name to authenticate.</param>
        /// <param name="logger">The configured logger.</param>
        /// <param name="tracer">The request tracer.</param>
        /// <param name="operationConfigurator">Operation configurator.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public ScramShaMechanism(MechanismType mechanismType,
            string password,
            string username,
            ILogger<ScramShaMechanism> logger,
            IRequestTracer tracer,
            IOperationConfigurator operationConfigurator)
        : base(tracer, operationConfigurator)
        {
            MechanismType = mechanismType;
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ClientNonce = GenerateClientNonce();
        }

        /// <summary>
        /// Gets or sets the client nonce.
        /// </summary>
        /// <value>
        /// The client nonce.
        /// </value>
        internal string ClientNonce { get; set; }

        public override async Task AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Internal.AuthenticateScramSha);
            try
            {
                // MechanismType is resolved authoritatively by SaslMechanismFactory before construction, using the
                // server's SASL_LIST_MECHS list cached on the connection (see ClientSupportedMechanisms /
                // TrySelectMechanism). The handshake below simply executes with the negotiated algorithm.
                var clientFirstMessage = "n,,n=" + _username + ",r=" + ClientNonce;
                var clientFirstMessageBare = clientFirstMessage.Substring(3);

                var serverFirstResult = await SaslStart(connection, clientFirstMessage, rootSpan, cancellationToken)
                    .ConfigureAwait(false);
                var serverFirstMessage = DecodeResponse(serverFirstResult);

                var serverNonce = serverFirstMessage["r"];
                var salt = Convert.FromBase64String(serverFirstMessage["s"]);
                var iterationCount = Convert.ToInt32(serverFirstMessage["i"]);

                //normalize and salt the password using the salt and iteration count
                var normalizedPassword = _password.Normalize(NormalizationForm.FormKC);
                var saltedPassword = GetSaltedPassword(normalizedPassword, salt, iterationCount);

                //build the final client message
                var clientFinalMessageNoProof = "c=biws,r=" + serverNonce;
                var authMessage = $"{clientFirstMessageBare},{serverFirstResult},{clientFinalMessageNoProof}";
                var clientFinalMessage = clientFinalMessageNoProof + ",p=" +
                                         Convert.ToBase64String(GetClientProof(saltedPassword, authMessage));

                var finalServerResponse = await SaslStep(connection, clientFinalMessage, rootSpan, cancellationToken)
                    .ConfigureAwait(false);
                Logger!.LogInformation(LoggingEvents.AuthenticationEvent, finalServerResponse);
            }
            catch (AuthenticationFailureException e)
            {
                Logger!.LogError(LoggingEvents.AuthenticationEvent, e, "Authentication failed.");
                throw;
            }
        }

        /// <summary>
        /// Selects the strongest client-supported mechanism (<see cref="ClientSupportedMechanisms"/>, in
        /// strongest-first order) that appears in the server's space-delimited SASL_LIST_MECHS response.
        /// </summary>
        /// <param name="serverListRaw">The raw SASL_LIST_MECHS payload, e.g. <c>"SCRAM-SHA512 SCRAM-SHA256 PLAIN"</c>.</param>
        /// <param name="selected">The negotiated mechanism, when a common one is found.</param>
        /// <returns><c>true</c> if a mutually-supported mechanism was found; otherwise <c>false</c>.</returns>
        internal static bool TrySelectMechanism(string serverListRaw, out MechanismType selected)
        {
            var serverMechanisms = (serverListRaw ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var candidate in ClientSupportedMechanisms)
            {
                if (serverMechanisms.Contains(candidate.GetDescription()!, StringComparer.OrdinalIgnoreCase))
                {
                    selected = candidate;
                    return true;
                }
            }

            selected = default;
            return false;
        }

        /// <summary>
        /// Returns the <see cref="HashAlgorithmName"/> corresponding to the current mechanism type.
        /// </summary>
        private HashAlgorithmName GetHashAlgorithmName() => MechanismType switch
        {
            MechanismType.ScramSha512 => HashAlgorithmName.SHA512,
            MechanismType.ScramSha256 => HashAlgorithmName.SHA256,
            _ => HashAlgorithmName.SHA1
        };

        /// <summary>
        /// Derives a salted password via PBKDF2 using the algorithm negotiated by <see cref="MechanismType"/>.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <param name="salt">The salt.</param>
        /// <param name="iterationCount">Number of PBKDF2 iterations.</param>
        /// <returns>Derived key bytes.</returns>
        /// <remarks>
        /// On .NET 8+, <c>Rfc2898DeriveBytes.Pbkdf2</c> supports SHA-256 and SHA-512.
        /// On earlier runtimes (netstandard2.x), <see cref="Rfc2898DeriveBytes"/> only supports SHA-1;
        /// SHA-256 and SHA-512 SCRAM variants require .NET 8 or later.
        /// </remarks>
        internal byte[] GetSaltedPassword(string password, byte[] salt, int iterationCount)
        {
#if NET8_0_OR_GREATER
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterationCount, GetHashAlgorithmName(), ShaByteLength);
#else
            // Rfc2898DeriveBytes on netstandard only supports SHA-1 (PBKDF2-HMAC-SHA1).
            // ScramSha256 and ScramSha512 require .NET 8+. Guard here in case ScramShaMechanism
            // is constructed directly (bypassing SaslMechanismFactory) with a stronger algorithm.
#pragma warning disable CS0618 // ScramSha1 is obsolete but is the only valid mechanism on netstandard
            if (MechanismType != MechanismType.ScramSha1)
#pragma warning restore CS0618
                throw new NotSupportedException(
                    $"{MechanismType} requires .NET 8 or later. Use ScramSha256 or ScramSha512 on .NET 8+.");
            using var bytes = new Rfc2898DeriveBytes(password, salt, iterationCount);
            return bytes.GetBytes(ShaByteLength);
#endif
        }

        /// <summary>
        /// Splits the server response into a <see cref="IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        internal IDictionary<string, string> DecodeResponse(string message)
        {
            return message.Split(',').ToDictionary(att => att.Substring(0, 1), att => att.Substring(2));
        }

        /// <summary>
        /// Computes HMAC over <paramref name="data"/> using the algorithm negotiated by <see cref="MechanismType"/>.
        /// </summary>
        /// <param name="key">The HMAC key.</param>
        /// <param name="data">The data to authenticate.</param>
        /// <returns>HMAC bytes.</returns>
        internal byte[] ComputeHash(byte[] key, string data)
        {
            using HMAC hmac = MechanismType switch
            {
                MechanismType.ScramSha512 => new HMACSHA512(key),
                MechanismType.ScramSha256 => new HMACSHA256(key),
                _ => new HMACSHA1(key)
            };
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Computes the hash digest using the algorithm negotiated by <see cref="MechanismType"/>.
        /// </summary>
        /// <param name="key">The data to hash.</param>
        /// <returns>Hash bytes.</returns>
        internal byte[] ComputeDigest(byte[] key)
        {
            using HashAlgorithm sha = MechanismType switch
            {
                MechanismType.ScramSha512 => SHA512.Create(),
                MechanismType.ScramSha256 => SHA256.Create(),
                _ => SHA1.Create()
            };
            return sha.ComputeHash(key);
        }

        /// <summary>
        /// Computes the SCRAM ClientProof: <c>ClientKey XOR HMAC(H(ClientKey), authMessage)</c>.
        /// Both parties can verify the shared secret without transmitting it directly (RFC 5802 §3).
        /// </summary>
        /// <param name="saltedPassword">Output of <see cref="GetSaltedPassword"/>.</param>
        /// <param name="authMessage">The SCRAM auth-message string.</param>
        /// <returns>ClientProof bytes, base64-encoded and sent in the client-final-message.</returns>
        internal byte[] GetClientProof(byte[] saltedPassword, string authMessage)
        {
            var clientKey = ComputeHash(saltedPassword, ClientKey);
            var storedKey = ComputeDigest(clientKey);
            var clientSignature = ComputeHash(storedKey, authMessage);

            XorInPlace(clientKey, clientSignature);
            return clientKey;
        }

        /// <summary>
        /// XOR's the specified result with an operand, updating the result.
        /// </summary>
        /// <param name="result">The input and result.</param>
        /// <param name="other">The operand.</param>
        internal void XorInPlace(Span<byte> result, ReadOnlySpan<byte> other)
        {
            for (var i = 0; i < result.Length; ++i)
            {
                result[i] ^= other[i];
            }
        }

        /// <summary>
        /// Generates a random client nonce.
        /// </summary>
        /// <returns></returns>
        internal string GenerateClientNonce()
        {
            const int nonceLength = 21;

#if !SPAN_SUPPORT
            var bytes = new byte[nonceLength];
#else
            Span<byte> bytes = stackalloc byte[nonceLength];
#endif

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        #region tracing
        private IRequestSpan RootSpan(string operation)
        {
            var span = Tracer.RequestSpan(operation);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name);
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }
        #endregion

        #region [ License information          ]

        /* ************************************************************
         *
         *    @author Couchbase <info@couchbase.com>
         *    @copyright 2015 Couchbase, Inc.
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

    }
}
