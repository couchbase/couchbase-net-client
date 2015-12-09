using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using Couchbase.Utils;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Provides support for SASL CRAM-MD5 for password encryption between the client and server.
    /// </summary>
    internal class CramMd5Mechanism : ISaslMechanism
    {
        private static readonly ILogger Log = new LoggerFactory().CreateLogger<CramMd5Mechanism>();
        private IOStrategy _ioStrategy;
        private ITypeTranscoder _transcoder;


        /// <summary>
        /// Creates a <see cref="CramMd5Mechanism"/> object using a given <see cref="IOStrategy"/>.
        /// </summary>
        /// <param name="ioStrategy">The I/O strategy to use.</param>
        /// <param name="transcoder"></param>
        public CramMd5Mechanism(IOStrategy ioStrategy, ITypeTranscoder transcoder)
        {
            _ioStrategy = ioStrategy;
            _transcoder = transcoder;
        }

        /// <summary>
        /// Creates a <see cref="CramMd5Mechanism"/> object using a given username (which is a Couchbase Bucket) and password.
        /// </summary>
        /// <param name="username">The name of the Bucket you are connecting to.</param>
        /// <param name="password">The password for the Bucket.</param>
        public CramMd5Mechanism(string username, string password)
        {
            Username = username;
            Password = password;
        }

        /// <summary>
        /// Creates a <see cref="CramMd5Mechanism"/> object using a given username (which is a Couchbase Bucket) and password.
        /// </summary>
        /// <param name="ioStrategy">The <see cref="IOStrategy"/>to use for I/O.</param>
        /// <param name="username">The name of the Bucket you are connecting to.</param>
        /// <param name="password">The password for the Bucket.</param>
        /// <param name="transcoder"></param>
        public CramMd5Mechanism(IOStrategy ioStrategy, string username, string password, ITypeTranscoder transcoder)
        {
            _ioStrategy = ioStrategy;
            Username = username;
            Password = password;
            _transcoder = transcoder;
        }

        /// <summary>
        /// The username or Bucket name.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// The password to authenticate against.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// The type of SASL mechanism to use: will always be CRAM-MD5.
        /// </summary>
        public string MechanismType
        {
            get { return "CRAM-MD5"; }
        }

        /// <summary>
        /// Authenticates a username and password using a specific <see cref="IConnection"/> instance. The password will
        /// be encrypted before being sent to the server.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <param name="username">The username or bucket name to authentic against.</param>
        /// <param name="password">The password to authenticate against.</param>
        /// <returns>True if succesful.</returns>
        public bool Authenticate(IConnection connection, string username, string password)
        {
            Username = username;
            Password = password ?? string.Empty;

            var temp = connection;

            var operation = new SaslStart(MechanismType, (VBucket)null, _transcoder, SaslFactory.DefaultTimeout);
            Log.Debug($"Authenticating socket {temp.Identity} with opaque {operation.Opaque}");

            var result = _ioStrategy.Execute(operation, connection);
            if (result.Status == ResponseStatus.AuthenticationContinue)
            {
                var challenge = result.Message;
                var reply = ComputeResponse(challenge);

                operation = new SaslStep(MechanismType, reply, _transcoder, SaslFactory.DefaultTimeout);
                result = _ioStrategy.Execute(operation, connection);
            }

            if (result.Status == ResponseStatus.AuthenticationError)
            {
                var tempResult = result;
                Log.Debug($"Authentication for socket {temp.Identity} failed: {tempResult.Message}");
            }
            else if (result.Status != ResponseStatus.Success)
            {
                var tempResult = result;
                Log.Debug($"Authentication for socket {temp.Identity} failed for a non-auth related reason: {tempResult.Message} - {tempResult.Status}");
                if (operation.Exception != null)
                {
                    Log.Debug($"Throwing exception for connection {temp.Identity}");
                    throw operation.Exception;
                }
            }
            else
            {
                Log.Debug($"Authentication for socket {temp.Identity} succeeded.");
            }

            return result.Status == ResponseStatus.Success;
        }

        /// <summary>
        /// Computes the reply or response to send back to the server that is hashed with the server's challenge.
        /// </summary>
        /// <param name="challenge">The key to hash the password against.</param>
        /// <returns>A reply to send back to the server.</returns>
        public string ComputeResponse(string challenge)
        {
            var data = string.IsNullOrWhiteSpace(challenge)
                ? new byte[0]
                : Encoding.ASCII.GetBytes(challenge);

            string hex;
			var encrypted = MacUtilities.CalculateMac("HMAC-MD5", new KeyParameter(Encoding.ASCII.GetBytes(Password)), data);
			hex = BitConverter.ToString(encrypted).
				Replace("-", String.Empty).
				ToLower();
            
            return string.Concat(Username, " ", hex);
        }

        /// <summary>
        /// Authenticates a username and password.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <returns>True if succesful.</returns>
        public bool Authenticate(IConnection connection)
        {
            return Authenticate(connection, Username, Password);
        }

        /// <summary>
        /// The <see cref="IOStrategy"/> to use for I/O connectivity with the Couchbase cluster or server.
        /// </summary>
        [Obsolete]
        public IOStrategy IOStrategy
        {
            set { _ioStrategy = value; }
        }
    }
}
