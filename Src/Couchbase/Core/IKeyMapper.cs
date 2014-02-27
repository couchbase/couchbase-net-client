using System.Security.Cryptography;

namespace Couchbase.Core
{
    internal interface IKeyMapper
    {
        IVBucket MapKey(string key);

        HashAlgorithm HashAlgorithm { get; set; }
    }
}
