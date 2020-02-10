using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Provides a SCRAM-SHA1 authentication implementation for Couchbase Server 4.5 and greater.
    /// </summary>
    /// <seealso cref="ISaslMechanism" />
    internal class ScramShaMechanism : SaslMechanismBase
    {
        private static readonly string ClientKey = "Client Key";
        private static readonly int ShaByteLength = 20;
        private readonly string _username;
        private readonly string _password;

        // private Func<string, object> User = RedactableArgument.UserAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScramShaMechanism"/> class.
        /// </summary>
        /// <param name="transcoder">The transcoder.</param>
        /// <param name="mechanismType">Type of the mechanism.</param>
        /// <param name="password">The password for the user.</param>
        /// <param name="username">The user's name to authenticate.</param>
        /// <param name="logger">The configured logger.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public ScramShaMechanism(ITypeTranscoder transcoder, MechanismType mechanismType, string password, string username, ILogger<ScramShaMechanism> logger)
        {
            Transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
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
            try
            {
                var clientFirstMessage = "n,,n=" + _username + ",r=" + ClientNonce;
                var clientFirstMessageBare = clientFirstMessage.Substring(3);

                var serverFirstResult = await SaslStart(connection, clientFirstMessage, cancellationToken).ConfigureAwait(false);
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
                var clientFinalMessage = clientFinalMessageNoProof + ",p=" + Convert.ToBase64String(GetClientProof(saltedPassword, authMessage));

                var finalServerResponse = await SaslStep(connection, clientFinalMessage, cancellationToken).ConfigureAwait(false);
                Logger.LogInformation(LoggingEvents.AuthenticationEvent, finalServerResponse);
            }
            catch (AuthenticationFailureException e)
            {
                Logger.LogError(LoggingEvents.AuthenticationEvent, e, "Authentication failed.");
                throw;
            }
        }

        /// <summary>
        /// Gets the salted password using <see cref="Rfc2898DeriveBytes"/> - SHA1 only!
        /// </summary>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        internal byte[] GetSaltedPassword(string password, byte[] salt, int iterationCount)
        {
            //.NET only officially supports SHA1 of PBKDF2 - a later commit could allow
            //support using the PBKDF2 class included in this patchset which supports SHA256
            //and SHA512 - given caveat emptor!
            using var bytes = new Rfc2898DeriveBytes(password, salt, iterationCount);
            return bytes.GetBytes(ShaByteLength);
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
        /// Generate the HMAC with the given SHA algorithm
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        internal byte[] ComputeHash(byte[] key, string data)
        {
            using var hmac = new HMACSHA1(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Computes the digest using SHA1.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        internal byte[] ComputeDigest(byte[] key)
        {
            using var sha = SHA1.Create();
            return sha.ComputeHash(key);
        }

        /// <summary>
        /// Gets the client proof so that the client and server can "prove" they have the same auth variable.
        /// </summary>
        /// <returns></returns>
        internal byte[] GetClientProof(byte[] saltedPassword, string authMessage)
        {
            var clientKey = ComputeHash(saltedPassword, ClientKey);
            var storedKey = ComputeDigest(clientKey);
            var clientSignature = ComputeHash(storedKey, authMessage);

            return XOR(clientKey, clientSignature);
        }

        /// <summary>
        /// XOR's the specified result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        internal byte[] XOR(byte[] result, byte[] other)
        {
            var buffer = new byte[result.Length];
            for (var i = 0; i < result.Length; ++i)
            {
                buffer[i] = (byte)(result[i] ^ other[i]);
            }
            return buffer;
        }

        /// <summary>
        /// Generates a random client nonce.
        /// </summary>
        /// <returns></returns>
        internal string GenerateClientNonce()
        {
            const int nonceLength = 21;
            var bytes = new byte[nonceLength];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

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
