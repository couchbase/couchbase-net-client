using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Resolves a bootstrap URI to a list of servers using DNS SRV lookup.
    /// </summary>
    public interface IDnsResolver
    {
        /// <summary>
        /// Resolve a bootstrap URI to a list of servers using DNS SRV lookup.
        /// </summary>
        /// <param name="bootstrapUri">Bootstrap URI to lookup.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of <seealso cref="Uri"/> objects, empty if the DNS SRV lookup fails.</returns>
        Task<IEnumerable<Uri>> GetDnsSrvEntriesAsync(Uri bootstrapUri,
            CancellationToken cancellationToken = default);
    }
}
