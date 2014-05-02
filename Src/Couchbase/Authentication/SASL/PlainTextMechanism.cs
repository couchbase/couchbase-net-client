using Common.Logging;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using System;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// A PLAIN text implementation of <see cref="ISaslMechanism"/> for authening connections to Couchbase Buckets. 
    /// </summary>
    internal sealed class PlainTextMechanism : ISaslMechanism
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private IOStrategy _strategy;

        public PlainTextMechanism(IOStrategy strategy)
        {
            _strategy = strategy;
        }

        public PlainTextMechanism(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public PlainTextMechanism(IOStrategy strategy, string username, string password)
        {
            _strategy = strategy;
            Username = username;
            Password = password;
        }

        /// <summary>
        /// The I/O strategy to use <see cref="IOStrategy"/>
        /// </summary>
        public IOStrategy IOStrategy
        {
            set { _strategy = value; }
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
        /// The type of SASL mechanism to use: PLAIN or CRAM MD5.
        /// </summary>
        public string MechanismType
        {
            get { return "PLAIN"; }
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
        /// Authenticates a username and password using a specific <see cref="IConnection"/> instance.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <param name="username">The username or bucket name to authentic against.</param>
        /// <param name="password">The password to authenticate against.</param>
        /// <returns>True if succesful.</returns>
        public bool Authenticate(IConnection connection, string username, string password)
        {
            var authenticated = false;
            var temp = connection;
            Log.Debug(m => m("Authenticating socket {0}", temp.Identity));

            try
            {
                var operation = new SaslAuthenticate(MechanismType, username, password);
                var result = _strategy.Execute(operation, connection);

                if (!result.Success &&
                    result.Status == ResponseStatus.AuthenticationError)
                {
                    Log.Debug(m => m("Authentication for socket {0} failed: {1}", temp.Identity, result.Value));
                }
                else
                {
                    authenticated = true;
                    Log.Debug(m => m("Authenticated socket {0} succeeded: {1}", temp.Identity, result.Value));
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return authenticated;
        }
    }
}
