using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IViewIndexes
    {
        ViewIndex Get();

        IEnumerable<ViewIndex> GetAll();

        Task Insert(string indexName, ViewIndexOptions options);

        Task Upsert(string indexName, ViewIndexOptions options);

        Task Remove(string indexName);
    }
}
