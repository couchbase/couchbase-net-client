using System.Threading;

namespace Couchbase.Management
{
    public class UpsertGroupOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public UpsertGroupOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UpsertGroupOptions Default => new UpsertGroupOptions();
    }
}