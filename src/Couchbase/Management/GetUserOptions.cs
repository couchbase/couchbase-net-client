using System.Threading;

namespace Couchbase.Management
{
    public class GetUserOptions
    {
        public string DomainName { get; set; } = "local";
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetUserOptions WithDomainName(string domainName)
        {
            DomainName = domainName;
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