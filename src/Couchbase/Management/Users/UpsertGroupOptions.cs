using System.Threading;

namespace Couchbase.Management.Users
{
    public class UpsertGroupOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public UpsertGroupOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UpsertGroupOptions Default => new UpsertGroupOptions();
    }
}
