using System.Threading;

namespace Couchbase.Management.Users
{
    public class GetAllUsersOptions
    {
        public string DomainName { get; set; } = "local";
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetAllUsersOptions WithDomainName(string domainName)
        {
            DomainName = domainName;
            return this;
        }

        public GetAllUsersOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllUsersOptions Default => new GetAllUsersOptions();
    }
}
