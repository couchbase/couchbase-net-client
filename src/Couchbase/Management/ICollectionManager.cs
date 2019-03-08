using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface ICollectionManager
    {
        Task Insert(string collectionName, CollectionManagerOptions options);

        Task Upsert(string collectionName, CollectionManagerOptions options); 

        Task Remove(string collectionName);
    }

    public class CollectionManagerOptions
    {
    }
}
