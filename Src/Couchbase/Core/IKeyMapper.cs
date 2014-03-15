using System.Security.Cryptography;

namespace Couchbase.Core
{
    internal interface IKeyMapper
    {
        IMappedNode MapKey(string key);

        HashAlgorithm HashAlgorithm { get; set; }
    }
}
