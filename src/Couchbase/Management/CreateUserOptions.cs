using System.Threading;

namespace Couchbase.Management
{
    public class CreateUserOptions
    {
        public AuthenticationDomain AuthenticationDomain { get; set; } = AuthenticationDomain.Local;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public CreateUserOptions WithAuthenticationDomain(AuthenticationDomain authenticationDomain)
        {
            AuthenticationDomain = authenticationDomain;
            return this;
        }

        public CreateUserOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreateUserOptions Default => new CreateUserOptions();
    }
}
