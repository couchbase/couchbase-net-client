using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

namespace Couchbase.Management.Views
{
    public interface IViewIndexManager
    {
        Task<DesignDocument> GetDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, GetDesignDocumentOptions options = null);

        Task<IEnumerable<DesignDocument>> GetAllDesignDocumentsAsync(DesignDocumentNamespace @namespace, GetAllDesignDocumentsOptions options = null);

        Task UpsertDesignDocumentAsync(DesignDocument indexData, DesignDocumentNamespace @namespace, UpsertDesignDocumentOptions options = null);

        Task DropDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, DropDesignDocumentOptions options = null);

        Task PublishDesignDocumentAsync(string designDocName, PublishDesignDocumentOptions options = null);
        }
}
