using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IQueryIndexes
    {
        QueryIndex Get(string indexName);

        IEnumerable<QueryIndex> GetAll();

        Task BuildDeferred();

        Task Watch();
            
        Task Create(string indexName, QueryIndexOptions options);

        Task Drop(string indexName);

        Task CreatePrimary(string indexName, QueryIndexOptions options);

        Task DropPrimary(string indexName);
    }

    public class QueryIndex
    {
    }

    public class QueryIndexOptions
    {
    }
}
