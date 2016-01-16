using System.ServiceModel.Syndication;

namespace Couchbase.Core
{
    public interface ILookupInBuilder
    {
        ILookupInBuilder Get(string path);

        ILookupInBuilder Exists(string path);

        IDocumentFragment<TContent> Execute<TContent>();

        string Key { get; set; }
    }
}