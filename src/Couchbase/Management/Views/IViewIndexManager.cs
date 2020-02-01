using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Views
{
    public interface IViewIndexManager
    {
        Task<DesignDocument> GetDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, GetDesignDocumentOptions? options = null);

        Task<IEnumerable<DesignDocument>> GetAllDesignDocumentsAsync(DesignDocumentNamespace @namespace, GetAllDesignDocumentsOptions? options = null);

        Task UpsertDesignDocumentAsync(DesignDocument indexData, DesignDocumentNamespace @namespace, UpsertDesignDocumentOptions? options = null);

        Task DropDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, DropDesignDocumentOptions? options = null);

        Task PublishDesignDocumentAsync(string designDocName, PublishDesignDocumentOptions? options = null);
        }
}
