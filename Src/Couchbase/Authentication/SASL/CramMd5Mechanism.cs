using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;

namespace Couchbase.Authentication.SASL
{
    internal class CramMd5Mechanism : ISaslMechanism
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private IOStrategy _ioStrategy;

        public CramMd5Mechanism(IOStrategy ioStrategy)
        {
            _ioStrategy = ioStrategy;
        }

        public CramMd5Mechanism(IOStrategy ioStrategy, string username, string password)
        {
            _ioStrategy = ioStrategy;
            Username = username;
            Password = password;
        }

        public string Username { get; private set; }

        public string Password { get; private set; }

        public string MechanismType
        {
            get { return "CRAM-MD5"; }
        }

        public bool Authenticate(IConnection connection, string username, string password)
        {
            var authenticated = false;
            Username = username;
            Password = password;
           
            var temp = connection;
            Log.Debug(m => m("Authenticating socket {0}", temp.Identity));

            var operation = new SaslStart(MechanismType, null);
            var result = _ioStrategy.Execute(operation, connection);
            if (result.Status == ResponseStatus.AuthenticationContinue)
            {
                var challenge = result.Message;
                var reply = ComputeResponse(challenge);

                operation = new SaslContinue(MechanismType, reply);
                result = _ioStrategy.Execute(operation, connection);

                authenticated = result.Status == ResponseStatus.Success && 
                    result.Value.Equals("Authenticated");

                Log.Debug(m => m("Authentication for socket {0} succeeded.", temp.Identity));
            }
           
            if (result.Status == ResponseStatus.AuthenticationError)
            {
                Log.Debug(m => m("Authentication for socket {0} failed: {1}", temp.Identity, result.Value));
            }

            return authenticated;
        }

        public string ComputeResponse(string challenge)
        {
            var data = string.IsNullOrWhiteSpace(challenge)
                ? new byte[0]
                : Encoding.ASCII.GetBytes(challenge);

            string hex;
            using (var hmac = new HMACMD5(Encoding.ASCII.GetBytes(Password)))
            {
                var encrypted = hmac.ComputeHash(data);
                hex = BitConverter.ToString(encrypted).
                    Replace("-", String.Empty).
                    ToLower();
            }
            return string.Concat(Username, " ", hex);
        }

        public bool Authenticate(IConnection connection)
        {
            return Authenticate(connection, Username, Password);
        }

        public IOStrategy IOStrategy
        {
            set { _ioStrategy = value; }
        }
    }
}
