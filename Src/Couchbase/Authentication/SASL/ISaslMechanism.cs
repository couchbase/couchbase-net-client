using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;

namespace Couchbase.Authentication.SASL
{
    interface ISaslMechanism
    {
        string Username { get; }

        string Password { get; }

        string MechanismType { get; }

        bool Authenticate(string username, string password);
    }
}
