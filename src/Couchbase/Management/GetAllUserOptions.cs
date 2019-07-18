using System.Threading;

namespace Couchbase.Management
{
    public class GetAllUserOptions
    {
        public AuthenticationDomain AuthenticationDomain { get; set; } = AuthenticationDomain.Local;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetAllUserOptions WithAuthenticationDomain(AuthenticationDomain authenticationDomain)
        {
            AuthenticationDomain = authenticationDomain;
            return this;
        }

        public GetAllUserOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllUserOptions Default => new GetAllUserOptions();
    }
}
