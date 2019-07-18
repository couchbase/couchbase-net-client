using System.ComponentModel;

namespace Couchbase.Management
{
    public enum AuthenticationDomain
    {
        [Description("local")]
        Local,

        [Description("external")]
        External
    }
}