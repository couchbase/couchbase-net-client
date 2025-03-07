#nullable enable

using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;

namespace Couchbase.KeyValue;

internal class TryLookupInResult:  TryResultBase, ITryLookupInResult
{
    private readonly ILookupInResult _lookupInResult;

    internal TryLookupInResult(ILookupInResult lookupInResult)
    {
               _lookupInResult = lookupInResult;
               var responseStatus = lookupInResult as IResponseStatus;
                Status = responseStatus!.Status;
    }

    public T? ContentAs<T>(int index)
    {
        if (((TryResultBase)(this)).DocumentExists)
        {
            return _lookupInResult.ContentAs<T>(index);
        }

        throw new DocumentNotFoundException();
    }
    public new bool Exists(int index) => _lookupInResult.Exists(index);

    public void Dispose() => _lookupInResult.Dispose();

    public ulong Cas => _lookupInResult.Cas;

    public int IndexOf(string key) => _lookupInResult.IndexOf(key);
    public bool IsDeleted => _lookupInResult.IsDeleted;

}
