using System.Threading;

namespace Couchbase.Management.Users
{
    public class AvailableRolesOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public AvailableRolesOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static AvailableRolesOptions Default => new AvailableRolesOptions();
    }
}
