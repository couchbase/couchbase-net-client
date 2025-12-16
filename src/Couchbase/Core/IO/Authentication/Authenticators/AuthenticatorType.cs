using System.ComponentModel;

namespace Couchbase.Core.IO.Authentication.Authenticators;

public enum AuthenticatorType
{
    [Description("Password")]
    Password,
    [Description("Certificate")]
    Certificate,
    [Description("JWT")]
    Jwt
}
