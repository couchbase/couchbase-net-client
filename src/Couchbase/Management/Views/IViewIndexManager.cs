using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Views;

namespace Couchbase.Management.Views
{
    public interface IViewIndexManager
    {
        Task<DesignDocument> GetDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, GetDesignDocumentOptions options);

        Task<IEnumerable<DesignDocument>> GetAllDesignDocumentsAsync(DesignDocumentNamespace @namespace, GetAllDesignDocumentsOptions options);

        Task UpsertDesignDocumentAsync(DesignDocument indexData, DesignDocumentNamespace @namespace, UpsertDesignDocumentOptions options);

        Task DropDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, DropDesignDocumentOptions options);

        Task PublishDesignDocumentAsync(string designDocName, PublishDesignDocumentOptions options);
    }
}
