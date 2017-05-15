using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Couchbase.Logging;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Operations.Authentication;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Provides a SCRAM-SHA1 authentication implementation for Couchbase Server 4.5 and greater.
    /// </summary>
    /// <seealso cref="Couchbase.Authentication.SASL.ISaslMechanism" />
    internal class ScramShaMechanism : ISaslMechanism
    {
        private static readonly ILog Log = LogManager.GetLogger<ScramShaMechanism>();
        private static readonly string ClientKey = "Client Key";
        private static readonly int ShaByteLength = 20;
        private ErrorMap _errorMap;

        //leaving for later patchset to support SHA256 and SHA512
        private static readonly Dictionary<string, string> _hmacs = new Dictionary<string, string>
        {
            {SASL.MechanismType.ScramSha512, "System.Security.Cryptography.HMACSHA512" },
            {SASL.MechanismType.ScramSha256, "System.Security.Cryptography.HMACSHA256" },
            {SASL.MechanismType.ScramSha1, "System.Security.Cryptography.HMACSHA1" }
        };

        private readonly ITypeTranscoder _transcoder;

        public byte[] Salt { get; private set; }
        public byte[] SaltedPassword { get; private set; }
        public int IterationCount { get; private set; }
        public string ClientFirstMessage { get; private set; }
        public string ClientFirstMessageBare { get; private set; }
        public string ClientFinalMessageNoProof { get; private set; }
        public string ClientFinalMessage { get; private set; }
        public string ServerFirstMessage { get; private set; }
        public string ServerFinalMessage { get; private set; }
        public string ServerNonce { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScramShaMechanism"/> class.
        /// </summary>
        /// <param name="transcoder">The transcoder.</param>
        /// <param name="mechanismType">Type of the mechanism.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public ScramShaMechanism(ITypeTranscoder transcoder, string mechanismType)
        {
            if (transcoder == null)
            {
                throw new ArgumentNullException("transcoder");
            }

            _transcoder = transcoder;
            MechanismType = mechanismType;
            ClientNonce = GenerateClientNonce();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScramShaMechanism"/> class.
        /// </summary>
        /// <param name="transcoder">The transcoder.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="mechanismType">Type of the mechanism.</param>
        /// <exception cref="System.ArgumentNullException">
        /// username
        /// or
        /// mechanismType
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public ScramShaMechanism(ITypeTranscoder transcoder, string username, string password, string mechanismType)
            : this(transcoder, mechanismType)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentNullException("username");
            }
            if (string.IsNullOrWhiteSpace(mechanismType))
            {
                throw new ArgumentNullException("mechanismType");
            }
            if (mechanismType == "CRAM-MD5" || mechanismType == "PLAIN")
            {
                throw new ArgumentOutOfRangeException(mechanismType == "CRAM-MD5"
                    ? "CRAM-MD5"
                    : "PLAIN");
            }
            Username = username;
            Password = password ?? string.Empty;
        }

        /// <summary>
        /// The username or Bucket name.
        /// </summary>
        public string Username { get; internal set; }

        /// <summary>
        /// The password to authenticate against.
        /// </summary>
        public string Password { get; internal set; }

        /// <summary>
        /// The type of SASL mechanism to use: SCRAM-SHA1.
        /// </summary>
        public string MechanismType { get; internal set; }

        /// <summary>
        /// Gets or sets the client nonce.
        /// </summary>
        /// <value>
        /// The client nonce.
        /// </value>
        internal string ClientNonce { get; set; }

        public bool Authenticate(IConnection connection, string username, string password)
        {
            var authenticated = false;
            ClientFirstMessage = "n,,n=" + username + ",r=" + ClientNonce;
            ClientFirstMessageBare = ClientFirstMessage.Substring(3);

            Log.Debug("Client First Message {0} - {1}: {2} [U:{3}|P:{4}", connection.EndPoint, connection.Identity, ClientFirstMessage, username, password);
            var authOp = new SaslStart(MechanismType, ClientFirstMessage, _transcoder, SaslFactory.DefaultTimeout);
            var serverFirstResult = Execute(authOp, connection);
            if (serverFirstResult.Status == ResponseStatus.AuthenticationContinue)
            {
                Log.Debug("Server First Message {0} - {1}: {2}", connection.EndPoint, connection.Identity, serverFirstResult.Message);

                //get the server nonce, salt and iterationcount from the server
                var serverFirstMessage = DecodeResponse(serverFirstResult.Message);
                ServerNonce = serverFirstMessage["r"];
                Salt = Convert.FromBase64String(serverFirstMessage["s"]);
                IterationCount = Convert.ToInt32(serverFirstMessage["i"]);

                //normalize and salt the password using the salt and iteration count
                var normalizedPassword = password.Normalize(NormalizationForm.FormKC);
                SaltedPassword = GetSaltedPassword(normalizedPassword);

                //build the final client message
                ClientFinalMessageNoProof = "c=biws,r=" + ServerNonce;
                ClientFinalMessage = ClientFinalMessageNoProof + ",p=" + Convert.ToBase64String(GetClientProof());
                Log.Debug("Client Final Message {0} - {1}: {2}", connection.EndPoint, connection.Identity, ClientFinalMessage);

                //send the final client message
                authOp = new SaslStep(MechanismType, ClientFinalMessage, _transcoder, SaslFactory.DefaultTimeout);
                var serverFinalResult  = Execute(authOp, connection);
                Log.Debug("Server Final Message {0} - {1}: {2}", connection.EndPoint, connection.Identity, serverFinalResult.Message);
                authenticated = serverFinalResult.Status == ResponseStatus.Success;
            }
            return authenticated;
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            var request = operation.Write();
            var response = connection.Send(request);
            operation.Read(response, _errorMap);
            return operation.GetResultWithValue();
        }

        /// <summary>
        /// Gets the salted password using <see cref="Rfc2898DeriveBytes"/> - SHA1 only!
        /// </summary>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        internal byte[] GetSaltedPassword(string password)
        {
            //.NET only officially supports SHA1 of PBKDF2 - a later commit could allow
            //support using the PBKDF2 class included in this patchset which supports SHA256
            //and SHA512 - given caveat emptor!
            using (var bytes = new Rfc2898DeriveBytes(password, Salt, IterationCount))
            {
                return bytes.GetBytes(ShaByteLength);
            }
        }

        /// <summary>
        /// Splits the server response into a <see cref="IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        internal IDictionary<string, string> DecodeResponse(string message)
        {
            ServerFirstMessage = message;
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
            using (var hmac = new HMACSHA1(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            }
        }

        /// <summary>
        /// Computes the digest using SHA1.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        internal byte[] ComputeDigest(byte[] key)
        {
            using (var sha = SHA1.Create())
            {
                return sha.ComputeHash(key);
            }
        }

        /// <summary>
        /// Gets the client proof so that the client and server can "prove" they have the same auth variable.
        /// </summary>
        /// <returns></returns>
        internal byte[] GetClientProof()
        {
            var clientKey = ComputeHash(SaltedPassword, ClientKey);
            var storedKey = ComputeDigest(clientKey);
            var clientSignature = ComputeHash(storedKey, GetAuthMessage());

            return XOR(clientKey, clientSignature);
        }

        /// <summary>
        /// Gets the authentication message.
        /// </summary>
        /// <returns></returns>
        internal string GetAuthMessage()
        {
            return string.Format("{0},{1},{2}", ClientFirstMessageBare, ServerFirstMessage, ClientFinalMessageNoProof);
        }

        /// <summary>
        /// Authenticates a username and password.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection" /> which represents a TCP connection to a Couchbase Server.</param>
        /// <returns>
        /// True if succesful.
        /// </returns>
        public bool Authenticate(IConnection connection)
        {
            return Authenticate(connection, Username, Password);
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
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
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
