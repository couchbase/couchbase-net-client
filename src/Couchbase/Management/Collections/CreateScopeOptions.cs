using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class CreateScopeOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public CreateScopeOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static CreateScopeOptions Default => new CreateScopeOptions();
    }
}
