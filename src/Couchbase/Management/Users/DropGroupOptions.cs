using System.Threading;

#nullable enable

namespace Couchbase.Management.Users
{
    public class DropGroupOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public DropGroupOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropGroupOptions Default => new DropGroupOptions();
    }
}
