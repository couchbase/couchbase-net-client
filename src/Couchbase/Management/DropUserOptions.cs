using System.Threading;

namespace Couchbase.Management
{
    public class DropUserOptions
    {
        public string DomainName { get; set; } = "local";
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public DropUserOptions WithDomainName(string domainName)
        {
            DomainName = domainName;
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