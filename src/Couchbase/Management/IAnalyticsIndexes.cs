using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
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
