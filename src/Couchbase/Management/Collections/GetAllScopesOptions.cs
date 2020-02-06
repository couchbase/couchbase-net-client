using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class GetAllScopesOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllScopesOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static GetAllScopesOptions Default => new GetAllScopesOptions();
    }
}
