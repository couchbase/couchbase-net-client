using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Query
{
    public class CreatePrimaryQueryIndexOptions
    {
        internal  string IndexNameValue { get; set; }
        internal bool IgnoreIfExistsValue { get; set; }
        internal bool DeferredValue { get; set; }
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public CreatePrimaryQueryIndexOptions IndexName(string indexName)
        {
            IndexNameValue = indexName;
            return this;
        }

        public CreatePrimaryQueryIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public CreatePrimaryQueryIndexOptions Deferred(bool deferred)
        {
            DeferredValue = deferred;
            return this;
        }

        public CreatePrimaryQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static CreatePrimaryQueryIndexOptions Default => new CreatePrimaryQueryIndexOptions();
    }
}
