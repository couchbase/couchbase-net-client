using System.Threading;

#nullable enable

namespace Couchbase.Management.Users
{
    public class GetGroupOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetGroupOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetGroupOptions Default => new GetGroupOptions();
    }
}
