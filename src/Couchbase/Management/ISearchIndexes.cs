using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface ISearchIndexes
    {
        SearchIndex Get();

        IEnumerable<SearchIndex> GetAll();

        Task Insert(string indexName, SearchIndexOptions options);

        Task Upsert(string indexName, SearchIndexOptions options);

        Task Remove(string indexName);
    }
}
