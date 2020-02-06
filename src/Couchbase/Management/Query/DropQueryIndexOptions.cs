using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class DropQueryIndexOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public DropQueryIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public DropQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropQueryIndexOptions Default => new DropQueryIndexOptions();
    }
}
