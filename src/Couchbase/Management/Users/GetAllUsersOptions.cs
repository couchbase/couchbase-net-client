using System.Threading;

#nullable enable

namespace Couchbase.Management.Users
{
    public class GetAllUsersOptions
    {
        internal string DomainNameValue { get; set; } = "local";
        internal CancellationToken TokenValue { get; set; }

        public GetAllUsersOptions DomainName(string domainName)
        {
            DomainNameValue = domainName;
            return this;
        }

        public GetAllUsersOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllUsersOptions Default => new GetAllUsersOptions();
    }
}
