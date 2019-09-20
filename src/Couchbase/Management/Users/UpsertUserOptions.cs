using System.Threading;

namespace Couchbase.Management.Users
{
    public class UpsertUserOptions
    {
        public string DomainName { get; set; } = "local";
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public UpsertUserOptions WithDomainName(string domainName)
        {
            DomainName = domainName;
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
