using System;

namespace Couchbase.Core
{
    public interface ILookupInBuilder<TDocument>
    {
        ILookupInBuilder<TDocument> Get(string path);

        ILookupInBuilder<TDocument> Exists(string path);

        IDocumentFragment<TDocument> Execute();

        string Key { get; set; }
    }
}