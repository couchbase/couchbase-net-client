using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Views
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
