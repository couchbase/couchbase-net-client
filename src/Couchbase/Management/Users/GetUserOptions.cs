using System.Threading;

namespace Couchbase.Management.Users
{
    public class GetUserOptions
    {
        public string DomainNameValue { get; set; } = "local";
        internal CancellationToken TokenValue { get; set; }

        public GetUserOptions DomainName(string domainName)
        {
            DomainNameValue = domainName;
            return this;
        }

        public GetUserOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetUserOptions Default => new GetUserOptions();
    }
}
