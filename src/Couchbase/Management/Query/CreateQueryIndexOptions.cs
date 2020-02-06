using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class CreateQueryIndexOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal bool DeferredValue { get; set; }
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public CreateQueryIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public CreateQueryIndexOptions Deferred(bool deferred)
        {
            DeferredValue = deferred;
            return this;
        }

        public CreateQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static CreateQueryIndexOptions Default => new CreateQueryIndexOptions();
    }
}
