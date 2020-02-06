using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class ScopeExistsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public ScopeExistsOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static ScopeExistsOptions Default => new ScopeExistsOptions();
    }
}
