using System.Threading;

namespace Couchbase.Management
{
    public class DropUserOptions
    {
        public AuthenticationDomain AuthenticationDomain { get; set; } = AuthenticationDomain.Local;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public DropUserOptions WithAuthenticationDomain(AuthenticationDomain authenticationDomain)
        {
            AuthenticationDomain = authenticationDomain;
            return this;
        }

        public DropUserOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropUserOptions Default => new DropUserOptions();
    }
}
