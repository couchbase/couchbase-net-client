using System.Threading;

namespace Couchbase.Management
{
    public class CreatePrimaryQueryIndexOptions
    {
        public  string IndexName { get; set; }
        public bool IgnoreIfExists { get; set; }
        public bool Deferred { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public CreatePrimaryQueryIndexOptions WithIndexName(string indexName)
        {
            IndexName = indexName;
            return this;
        }

        public CreatePrimaryQueryIndexOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public CreatePrimaryQueryIndexOptions WithDeferred(bool deferred)
        {
            Deferred = deferred;
            return this;
        }

        public CreatePrimaryQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreatePrimaryQueryIndexOptions Default => new CreatePrimaryQueryIndexOptions();
    }
}