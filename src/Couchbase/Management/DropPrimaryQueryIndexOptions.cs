using System.Threading;

namespace Couchbase.Management
{
    public class DropPrimaryQueryIndexOptions
    {
        public string IndexName { get; set; }
        public bool IgnoreIfExists { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public DropPrimaryQueryIndexOptions WithIndexName(string indexName)
        {
            IndexName = indexName;
            return this;
        }

        public DropPrimaryQueryIndexOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public DropPrimaryQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropPrimaryQueryIndexOptions Default => new DropPrimaryQueryIndexOptions();
    }
}