using System.Threading;

#nullable enable

namespace Couchbase.Management.Users
{
    public class DropUserOptions
    {
        internal string DomainNameValue { get; set; } = "local";
        internal CancellationToken TokenValue { get; set; }

        public DropUserOptions DomainName(string domainName)
        {
            DomainNameValue = domainName;
            return this;
        }

        public DropUserOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropUserOptions Default => new DropUserOptions();
    }
}
