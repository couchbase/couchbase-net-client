using System.Threading;

namespace Couchbase.Management.Collections
{
    public class DropScopeOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public DropScopeOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static DropScopeOptions Default => new DropScopeOptions();
    }
}
