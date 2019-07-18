using System.Threading;

namespace Couchbase.Management
{
    public class GetUserOptions
    {
        public AuthenticationDomain AuthenticationDomain { get; set; } = AuthenticationDomain.Local;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetUserOptions WithAuthenticationDomain(AuthenticationDomain authenticationDomain)
        {
            AuthenticationDomain = authenticationDomain;
            return this;
        }

        public GetUserOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetUserOptions Default => new GetUserOptions();
    }
}