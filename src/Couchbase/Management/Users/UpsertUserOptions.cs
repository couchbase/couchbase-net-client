using System.Threading;

namespace Couchbase.Management.Users
{
    public class UpsertUserOptions
    {
        internal string DomainNameValue { get; set; } = "local";
        internal CancellationToken TokenValue { get; set; }

        public UpsertUserOptions DomainName(string domainName)
        {
            DomainNameValue = domainName;
            return this;
        }

        public UpsertUserOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UpsertUserOptions Default => new UpsertUserOptions();
    }
}
