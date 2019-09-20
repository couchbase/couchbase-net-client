using System.Threading;

namespace Couchbase.Management.Query
{
    public class CreateQueryIndexOptions
    {
        public bool IgnoreIfExists { get; set; }
        public bool Deferred { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public CreateQueryIndexOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public CreateQueryIndexOptions WithDeferred(bool deferred)
        {
            Deferred = deferred;
            return this;
        }

        public CreateQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreateQueryIndexOptions Default => new CreateQueryIndexOptions();
    }
}
