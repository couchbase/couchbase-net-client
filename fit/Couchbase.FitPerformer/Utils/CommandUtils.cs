using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.FitPerformer.Workload;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Google.Protobuf;
using MutateInSpec = Couchbase.KeyValue.MutateInSpec;
using StoreSemantics = Couchbase.KeyValue.StoreSemantics;

namespace Couchbase.FitPerformer.Utils;

public static class CommandUtils
{
    private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() => new Random(Environment.TickCount));
    public static ContentTypes DeserializeContentType(Couchbase.Grpc.Protocol.Shared.ContentAs.AsOneofCase contentAs, ILookupInResult result, int index)
    {
        ContentTypes ret = new ContentTypes();

        switch (contentAs)
        {
            case ContentAs.AsOneofCase.AsString:
                ret.ContentAsString = result.ContentAs<string>(index);
                break;
            case ContentAs.AsOneofCase.AsByteArray:
                ret.ContentAsBytes = ByteString.CopyFrom(result.ContentAs<byte[]>(index));
                break;
            case ContentAs.AsOneofCase.AsBoolean:
                ret.ContentAsBool = result.ContentAs<bool>(index);
                break;
            case ContentAs.AsOneofCase.AsInteger:
                ret.ContentAsInt64 = result.ContentAs<long>(index);
                break;
            case ContentAs.AsOneofCase.AsFloatingPoint:
                ret.ContentAsDouble = result.ContentAs<double>(index);
                break;
            case ContentAs.AsOneofCase.AsJsonArray:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>(index)?.ToString());
                break;
            case ContentAs.AsOneofCase.AsJsonObject:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>(index)?.ToString());
                break;
            default:
                throw new ArgumentException("Unknown ContentType case.");
        }
        return ret;
    }

    public static ContentTypes DeserializeContentType(Couchbase.Grpc.Protocol.Shared.ContentAs.AsOneofCase contentAs, IScanResult result)
    {
        ContentTypes ret = new ContentTypes();

        switch (contentAs)
        {
            case ContentAs.AsOneofCase.AsString:
                ret.ContentAsString = result.ContentAs<string>();
                break;
            case ContentAs.AsOneofCase.AsByteArray:
                ret.ContentAsBytes = ByteString.CopyFrom(result.ContentAsBytes());
                break;
            case ContentAs.AsOneofCase.AsBoolean:
                ret.ContentAsBool = result.ContentAs<bool>();
                break;
            case ContentAs.AsOneofCase.AsInteger:
                ret.ContentAsInt64 = result.ContentAs<long>();
                break;
            case ContentAs.AsOneofCase.AsFloatingPoint:
                ret.ContentAsDouble = result.ContentAs<double>();
                break;
            case ContentAs.AsOneofCase.AsJsonArray:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>()?.ToString());
                break;
            case ContentAs.AsOneofCase.AsJsonObject:
                // we could decode the content as UTF8, parse as JSON, and re-string it, but...
                // if it's already JSON, just copy the bytes over.
                ret.ContentAsBytes = ByteString.CopyFrom(result.ContentAsBytes());
                break;
            default:
                throw new ArgumentException("Unknown ContentType case.");
        }
        return ret;
    }

    public static ContentTypes DeserializeContentType(Couchbase.Grpc.Protocol.Shared.ContentAs.AsOneofCase contentAs, IGetResult result)
    {
        ContentTypes ret = new ContentTypes();

        switch (contentAs)
        {
            case ContentAs.AsOneofCase.AsString:
                ret.ContentAsString = result.ContentAs<string>();
                break;
            case ContentAs.AsOneofCase.AsByteArray:
                ret.ContentAsBytes = ByteString.CopyFrom(result.ContentAs<byte[]>());
                break;
            case ContentAs.AsOneofCase.AsBoolean:
                ret.ContentAsBool = result.ContentAs<bool>();
                break;
            case ContentAs.AsOneofCase.AsInteger:
                ret.ContentAsInt64 = result.ContentAs<long>();
                break;
            case ContentAs.AsOneofCase.AsFloatingPoint:
                ret.ContentAsDouble = result.ContentAs<double>();
                break;
            case ContentAs.AsOneofCase.AsJsonArray:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>()?.ToString());
                break;
            case ContentAs.AsOneofCase.AsJsonObject:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>()?.ToString());
                break;
            default:
                throw new ArgumentException("Unknown ContentType case.");
        }
        return ret;
    }

    public static ContentTypes DeserializeContentType(Couchbase.Grpc.Protocol.Shared.ContentAs.AsOneofCase contentAs, IMutateInResult result, int index)
    {
        ContentTypes ret = new ContentTypes();
        switch (contentAs)
        {
            case ContentAs.AsOneofCase.AsString:
                ret.ContentAsString = result.ContentAs<string>(index);
                break;
            case ContentAs.AsOneofCase.AsByteArray:
                ret.ContentAsBytes = ByteString.CopyFrom(result.ContentAs<byte[]>(index));
                break;
            case ContentAs.AsOneofCase.AsBoolean:
                ret.ContentAsBool = result.ContentAs<bool>(index);
                break;
            case ContentAs.AsOneofCase.AsInteger:
                ret.ContentAsInt64 = result.ContentAs<long>(index);
                break;
            case ContentAs.AsOneofCase.AsFloatingPoint:
                ret.ContentAsDouble = result.ContentAs<double>(index);
                break;
            case ContentAs.AsOneofCase.AsJsonArray:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>(index)?.ToString());
                break;
            case ContentAs.AsOneofCase.AsJsonObject:
                ret.ContentAsBytes = ByteString.CopyFromUtf8(result.ContentAs<dynamic>(index)?.ToString());
                break;
            default:
                throw new ArgumentException("Unknown ContentType case.");
        }
        return ret;
    }

    public static async Task<(ICouchbaseCollection, string)> DetermineLocation(Couchbase.Grpc.Protocol.Shared.DocLocation location, ClusterConnection connection, Counters counters)
        {
            ICouchbaseCollection collection;
            string id;

            switch (location.LocationCase)
                {
                    case DocLocation.LocationOneofCase.Pool:
                    {
                        var preface = location.Pool.IdPreface;
                        long generated = 0;

                        if (location.Pool.PoolSelectionStrategyCase ==
                            DocLocationPool.PoolSelectionStrategyOneofCase.Random)
                        {
                            generated = Random.Value.NextInt64((int)location.Pool.PoolSize);
                        }
                        else
                        {
                            var counter = counters.GetCounter(location.Pool.Counter.Counter);
                            generated = counter.IncrementAndGet() % (int)location.Pool.PoolSize;
                        }

                        var col = location.Pool.Collection;

                        collection = await connection.GetCollectionAsync(col.BucketName, col.ScopeName, col.CollectionName).ConfigureAwait(false);
                        id = preface + generated;
                        break;
                    }
                    case DocLocation.LocationOneofCase.Specific:
                    {
                        var col = location.Specific.Collection;
                        collection = await connection.GetCollectionAsync(col.BucketName, col.ScopeName, col.CollectionName).ConfigureAwait(false);
                        id = location.Specific.Id;
                        break;
                    }
                    case DocLocation.LocationOneofCase.Uuid:
                    {
                        var col = location.Uuid.Collection;
                        collection = await connection.GetCollectionAsync(col.BucketName, col.ScopeName, col.CollectionName).ConfigureAwait(false);
                        id = Guid.NewGuid().ToString();
                        break;
                    }
                    default:
                        throw new UnsupportedException("No Location specified for LookupIn request.");

                }

            return (collection, id);
        }

    public static string GetDocId(DocLocation location, Counters counters)
    {
        if (location.LocationCase == DocLocation.LocationOneofCase.Specific)
        {
            return location.Specific.Id;
        }
        if (location.LocationCase == DocLocation.LocationOneofCase.Uuid)
        {
            return Guid.NewGuid().ToString();
        }
        if (location.LocationCase == DocLocation.LocationOneofCase.Pool)
        {
            var pool = location.Pool;

            long next;
            if (pool.PoolSelectionStrategyCase == DocLocationPool.PoolSelectionStrategyOneofCase.Random)
            {
                if (pool.Random.Distribution == RandomDistribution.Uniform) {
                    next = Random.Value.NextInt64((int)pool.PoolSize);
                }
                else {
                    throw new NotSupportedException();
                }
            }
            else if (pool.PoolSelectionStrategyCase == DocLocationPool.PoolSelectionStrategyOneofCase.Counter)
            {
                var counter = counters.GetCounter(pool.Counter.Counter);
                next = counter.IncrementAndGet() % (int)pool.PoolSize;
            }
            else
            {
                throw new NotSupportedException("Unrecognised pool selection strategy");
            }

            return pool.IdPreface + next;
        }

        throw new NotSupportedException("Unknown doc location type");

    }

    public static TimeSpan ConvertExpiry(Expiry expiry)
    {
        return expiry.ExpiryTypeCase switch
        {
            Expiry.ExpiryTypeOneofCase.RelativeSecs => TimeSpan.FromSeconds(expiry.RelativeSecs),
            // Used the more verbose version because for some reason DateTimeOffset.Offset always returns a timespan of 00:00:00
            // Expiry.ExpiryTypeOneofCase.AbsoluteEpochSecs => DateTimeOffset.FromUnixTimeSeconds(expiry.AbsoluteEpochSecs).Offset,
            Expiry.ExpiryTypeOneofCase.AbsoluteEpochSecs => TimeSpan.FromSeconds(DateTimeOffset.FromUnixTimeSeconds(expiry.AbsoluteEpochSecs).ToUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            _ => throw new NotSupportedException("Unknown expiry")
        };
    }

    public static StoreSemantics ConvertStoreSemantics(Couchbase.Grpc.Protocol.Sdk.Collection.MutateIn.StoreSemantics semantics)
    {
        switch (semantics)
        {
            case Grpc.Protocol.Sdk.Collection.MutateIn.StoreSemantics.Insert: return KeyValue.StoreSemantics.Insert;
            case Grpc.Protocol.Sdk.Collection.MutateIn.StoreSemantics.Upsert: return KeyValue.StoreSemantics.Upsert;
            case Grpc.Protocol.Sdk.Collection.MutateIn.StoreSemantics.Replace: return KeyValue.StoreSemantics.Replace;
            default: throw new NotSupportedException("Unknown Store Semantics");
        }
    }

    public static MutateInSpec ConvertMutateInSpec(Couchbase.Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec requestSpec)
    {

        switch (requestSpec.OperationCase)
        {
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.Upsert:
                return MutateInSpec.Upsert(
                    requestSpec.Upsert.Path,
                    ResultsUtil.ContentOrMacro(requestSpec.Upsert.Content),
                    createPath: requestSpec.Upsert.CreatePath,
                    isXattr:requestSpec.Upsert.Xattr);
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.Insert:
                return MutateInSpec.Insert(
                    requestSpec.Insert.Path,
                    ResultsUtil.ContentOrMacro(requestSpec.Insert.Content),
                    createPath: requestSpec.Insert.CreatePath,
                    isXattr: requestSpec.Insert.Xattr);
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.Replace:
                return MutateInSpec.Replace(
                    requestSpec.Replace.Path,
                    ResultsUtil.ContentOrMacro(requestSpec.Replace.Content),
                    isXattr:requestSpec.Replace.Xattr);
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.Remove:
                return MutateInSpec.Remove(
                    requestSpec.Remove.Path,
                    isXattr:requestSpec.Remove.Xattr);
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.ArrayAppend:
                return MutateInSpec.ArrayAppend(
                    requestSpec.ArrayAppend.Path,
                    requestSpec.ArrayAppend.Content.Select(content => ResultsUtil.ContentOrMacro(content)),
                    createPath: requestSpec.ArrayAppend.CreatePath,
                    isXattr:requestSpec.ArrayAppend.Xattr,
                    removeBrackets: true
                );
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.ArrayPrepend:
                return MutateInSpec.ArrayPrepend(
                    requestSpec.ArrayPrepend.Path,
                    requestSpec.ArrayPrepend.Content.Select(content => ResultsUtil.ContentOrMacro(content)),
                    createPath: requestSpec.ArrayPrepend.CreatePath,
                    isXattr:requestSpec.ArrayPrepend.Xattr,
                    removeBrackets: true
                );
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.ArrayInsert:
                return MutateInSpec.ArrayInsert(
                    requestSpec.ArrayInsert.Path,
                    requestSpec.ArrayInsert.Content.Select(content => ResultsUtil.ContentOrMacro(content)),
                    createPath: requestSpec.ArrayInsert.CreatePath,
                    isXattr:requestSpec.ArrayInsert.Xattr,
                    removeBrackets: true
                );
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.ArrayAddUnique:
                return MutateInSpec.ArrayAddUnique(
                    requestSpec.ArrayAddUnique.Path,
                    ResultsUtil.ContentOrMacro(requestSpec.ArrayAddUnique.Content),
                    createPath: requestSpec.ArrayAddUnique.CreatePath,
                    isXattr:requestSpec.ArrayAddUnique.Xattr
                );
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.Increment:
            {
                var spec = MutateInSpec.Increment(
                    requestSpec.Increment.Path,
                    requestSpec.Increment.Delta,
                    createPath: requestSpec.Increment.CreatePath,
                    isXattr:requestSpec.Increment.Xattr);

                spec = MutateInSpec.Increment(
                    requestSpec.Increment.Path,
                    (ulong)requestSpec.Increment.Delta,
                    createPath: requestSpec.Increment.CreatePath,
                    isXattr:requestSpec.Increment.Xattr);
                return spec;
            }
            case Grpc.Protocol.Sdk.Collection.MutateIn.MutateInSpec.OperationOneofCase.Decrement:
            {
                var spec = MutateInSpec.Decrement(
                    requestSpec.Decrement.Path,
                    requestSpec.Decrement.Delta,
                    createPath: requestSpec.Decrement.CreatePath,
                    isXattr:requestSpec.Decrement.Xattr);

                spec = MutateInSpec.Decrement(
                    requestSpec.Decrement.Path,
                    (ulong)requestSpec.Decrement.Delta,
                    createPath: requestSpec.Decrement.CreatePath,
                    isXattr:requestSpec.Decrement.Xattr);
                return spec;
            }
            default:
                throw new NotSupportedException("Given MutateInSpec operation is unsupported");
        }
    }
}