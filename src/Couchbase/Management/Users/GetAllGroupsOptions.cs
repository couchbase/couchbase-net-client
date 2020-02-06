using System.Threading;

#nullable enable

namespace Couchbase.Management.Users
{
    public class GetAllGroupsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllGroupsOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllGroupsOptions Default => new GetAllGroupsOptions();
    }
}
