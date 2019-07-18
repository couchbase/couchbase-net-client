using System.Threading;

namespace Couchbase.Management
{
    public class UpsertUserOptions
    {
        public string Password { get; set; }
        public AuthenticationDomain AuthenticationDomain { get; set; } = AuthenticationDomain.Local;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public UpsertUserOptions WithAuthenticationDomain(AuthenticationDomain authenticationDomain)
        {
            AuthenticationDomain = authenticationDomain;
            return this;
        }

        public UpsertUserOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UpsertUserOptions Default => new UpsertUserOptions();
    }
}
