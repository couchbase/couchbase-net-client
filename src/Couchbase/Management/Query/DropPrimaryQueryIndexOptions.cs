using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Query
{
    public class DropPrimaryQueryIndexOptions
    {
        internal string IndexNameValue { get; set; }
        internal bool IgnoreIfExistsValue { get; set; }
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public DropPrimaryQueryIndexOptions IndexName(string indexName)
        {
            IndexNameValue = indexName;
            return this;
        }

        public DropPrimaryQueryIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public DropPrimaryQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropPrimaryQueryIndexOptions Default => new DropPrimaryQueryIndexOptions();
    }
}
