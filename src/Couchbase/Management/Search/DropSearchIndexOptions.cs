using System.Threading;

#nullable enable

namespace Couchbase.Management.Search
{
    public class DropSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public DropSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropSearchIndexOptions Default => new DropSearchIndexOptions();
    }
}
