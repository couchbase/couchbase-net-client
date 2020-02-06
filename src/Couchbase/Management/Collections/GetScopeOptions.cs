using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class GetScopeOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetScopeOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static GetScopeOptions Default => new GetScopeOptions();
    }
}
