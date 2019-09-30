using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    public interface IViewManager
    {
        Task<DesignDocument> GetAsync(string designDocName, GetViewIndexOptions options);
        Task<IEnumerable<DesignDocument>> GetAllAsync(GetAllViewIndexOptions options);
        Task CreateAsync(DesignDocument designDocument, CreateViewIndexOptions options);
        Task UpsertAsync(DesignDocument designDocument, UpsertViewIndexOptions options);
        Task DropAsync(string designDocName, DropViewIndexOptions options);
        Task PublishAsync(string designDocName, PublishIndexOptions options);
    }
}
