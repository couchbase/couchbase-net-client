using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Search;

namespace Couchbase.Management.Analytics
{
    public interface IAnalyticsIndexes
    {
        SearchIndex Get();

        IEnumerable<AnalyticsIndex> GetAll();

        Task Insert(string indexName, AnalyticsIndexOptions options);

        Task Upsert(string indexName, AnalyticsIndexOptions options);

        Task Remove(string indexName);
    }

    public class AnalyticsIndex
    {
    }

    public class AnalyticsIndexOptions
    {
    }
}
