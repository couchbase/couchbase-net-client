using System;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;

namespace Couchbase.KeyValue;

/// <summary>
/// Provides an interface for supporting the state of a document if the server
/// returns a KeyNotFound status, as opposed to throwing a <see cref="DocumentNotFoundException"/>
/// like in the regular GetAsync methods.
/// </summary>
internal class TryGetResult: ITryGetResult
{
    private readonly IGetResult _getResult;

    internal TryGetResult(IGetResult getGetResult)
    {
        _getResult = getGetResult;
    }

    /// <inheritdoc />
    public bool Exists => _getResult.Status == ResponseStatus.Success;

    /// <inheritdoc />
    public ulong Cas => _getResult.Cas;

    /// <inheritdoc />
    public void Dispose()
    {
        _getResult.Dispose();
    }

    /// <inheritdoc />
    public T ContentAs<T>()
    {
        if (!Exists) throw new DocumentNotFoundException();
        return _getResult.ContentAs<T>();
    }

    /// <inheritdoc />
    public TimeSpan? Expiry => throw new NotSupportedException("Use ExpiryTime.");

    /// <inheritdoc />
    public DateTime? ExpiryTime => _getResult.ExpiryTime;

    ResponseStatus IGetResult.Status
    {
        get => _getResult.Status;
        set => throw new NotSupportedException();
    }
}
