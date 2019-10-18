using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase
{
    public interface IDnsResolver
    {
        Task<IEnumerable<Uri>> GetDnsSrvEntriesAsync(Uri bootstrapUri);
    }
}
