using System.Threading;

namespace Couchbase.Management.Query
{
    public class DropQueryIndexOptions
    {
        public bool IgnoreIfExists { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public DropQueryIndexOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public DropQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropQueryIndexOptions Default => new DropQueryIndexOptions();
    }
}
