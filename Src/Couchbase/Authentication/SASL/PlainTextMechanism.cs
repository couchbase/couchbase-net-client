using Common.Logging;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using System;

namespace Couchbase.Authentication.SASL
{
    internal class PlainTextMechanism : ISaslMechanism
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

        public IOStrategy IOStrategy
        {
            set { _strategy = value; }
        }

        public string Username { get; private set; }

        public string Password { get; private set; }

        public string MechanismType
        {
            get { return "PLAIN"; }
        }

        public bool Authenticate(IConnection connection)
        {
            return Authenticate(connection, Username, Password);
        }

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
