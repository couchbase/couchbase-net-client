using System.Threading;

namespace Couchbase.Management.Users
{
    public class DropGroupOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public DropGroupOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropGroupOptions Default => new DropGroupOptions();
    }
}
