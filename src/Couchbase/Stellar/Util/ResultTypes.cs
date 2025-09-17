#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Text;
using Couchbase;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.KeyValue;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using LookupInRequest = Couchbase.Protostellar.KV.V1.LookupInRequest;
using MutateInRequest = Couchbase.Protostellar.KV.V1.MutateInRequest;
using MutationToken = Couchbase.Core.MutationToken;

#region KV
namespace Couchbase.Stellar.KeyValue
{
    #nullable enable

    internal record ExistsResult(bool Exists, ulong Cas) : Couchbase.KeyValue.IExistsResult;
    internal record MutationResult(ulong Cas, TimeSpan? Expiry) : Couchbase.KeyValue.IMutationResult
    {
        private MutationToken _mutationToken = MutationToken.Empty;
        public MutationToken MutationToken
        {
            get => _mutationToken;
            set => _mutationToken = value;
        }
    }

    internal record GetReplicaResult(ulong Cas, bool IsActive, GrpcContentWrapper GrpcContentWrapper) : Couchbase.KeyValue.IGetReplicaResult
    {
        public void Dispose() { }

        public T? ContentAs<T>() => GrpcContentWrapper.ContentAs<T>();
        public TimeSpan? Expiry => null;
        public DateTime? ExpiryTime => null;
    }

    internal record GetResult(DateTime? ExpiryTime, ulong Cas, GrpcContentWrapper GrpcContentWrapper) : Couchbase.KeyValue.IGetResult
    {
        public TimeSpan? Expiry => null;
        public T? ContentAs<T>() => GrpcContentWrapper.ContentAs<T>();
        public void Dispose() { }
    }

    internal record LookupInResult(LookupInResponse GrpcResponse, LookupInRequest OriginalRequest, ITypeSerializer Serializer) : Couchbase.KeyValue.ILookupInResult
    {
        internal static readonly ReadOnlyMemory<byte> RawTrue = Encoding.ASCII.GetBytes("true");
        internal static readonly ReadOnlyMemory<byte> RawFalse = Encoding.ASCII.GetBytes("false");

        public ulong Cas => GrpcResponse.Cas;
        public bool IsDeleted => false; // Not supported in protostellar

        bool ILookupInResult.Exists(int index)
        {
            var spec = SpecOrInvalid(index);
            if (spec.Content == null)
            {
                return false;
            }

            if (spec.Status != null && spec.Status.Code != (int)StatusCode.OK)
            {
                return false;
            }

            var originalSpec = OriginalRequest.Specs[index];
            if (originalSpec.Operation == LookupInRequest.Types.Spec.Types.Operation.Exists)
            {
                var contentBytes = spec.Content.Span;
                if (contentBytes.SequenceEqual(RawTrue.Span))
                {
                    return true;
                }

                if (contentBytes.SequenceEqual(RawFalse.Span))
                {
                    return false;
                }

                throw new ArgumentOutOfRangeException("returnValue", "expected 'true' or 'false'");
            }

            // if the original spec was NOT an exists request, then any successful status is a 'true' result.
            return spec.Status == null || spec.Status.Code == (int)StatusCode.OK;
        }

        public T? ContentAs<T>(int index)
        {

            var spec = SpecOrInvalid(index);
            if (spec.Status == null || spec.Status.Code == (int)StatusCode.OK)
            {
                var contentWrapper = new GrpcContentWrapper(spec.Content, 0, this.Serializer);
                return contentWrapper.ContentAs<T>();
            }
            switch (spec.Status.Code)
            {
                case (int)StatusCode.InvalidArgument:
                    throw new InvalidArgumentException(spec.Status.Message);
                case (int)StatusCode.NotFound:
                    throw new PathNotFoundException();
                case (int)StatusCode.FailedPrecondition:
                    throw new PathMismatchException();
                default:
                    throw new CouchbaseException(spec.Status.Message);
            }
        }

        public int IndexOf(string path)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));
            for (int i = 0; i < OriginalRequest.Specs.Count; i++)
            {
                if (path == OriginalRequest.Specs[i].Path)
                {
                    return i;
                }
            }

            return -1;
        }

        private LookupInResponse.Types.Spec SpecOrInvalid(int index)
        {
            var specs = GrpcResponse.Specs;
            if (index < 0 || index >= specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = specs[index];
            return spec;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    internal record MutateInResult(MutateInResponse GrpcResponse, MutateInRequest OriginalRequest, ITypeSerializer Serializer) : IMutateInResult
    {

        public ulong Cas => GrpcResponse.Cas;

        public MutationToken MutationToken
        {
            get => GrpcResponse.MutationToken;
            set => throw new NotSupportedException();
        }

        public T? ContentAs<T>(int index)
        {
            var spec = SpecOrInvalid(index);
            var contentWrapper = new GrpcContentWrapper(spec.Content, 0, Serializer);
            return contentWrapper.ContentAs<T>();
        }

        public int IndexOf(string path)
        {
            // some copy/paste on these methods because GRPC/protobuf generates different types for Specs
            // with no common interface
            _ = path ?? throw new ArgumentNullException(nameof(path));
            for (int i = 0; i < OriginalRequest.Specs.Count; i++)
            {
                if (path == OriginalRequest.Specs[i].Path)
                {
                    return i;
                }
            }

            return -1;
        }

        private MutateInResponse.Types.Spec SpecOrInvalid(int index)
        {
            var specs = GrpcResponse.Specs;
            if (index < 0 || index >= specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = specs[index];
            return spec;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    internal interface IContentResult
    {
        Timestamp Expiry { get; }
        ByteString Content { get; }
        uint ContentFlags { get; }
        ulong Cas { get; }
    }

    internal interface IReplicaContentResult
    {
        ByteString Content { get; }
        uint ContentFlags { get; }
        ulong Cas { get; }
        bool IsActive { get; }
    }
}

namespace Couchbase.Protostellar.KV.V1
{
    partial class GetResponse : IContentResult, IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
    partial class GetAndLockResponse : IContentResult, IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
    partial class GetAndTouchResponse : IContentResult, IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class GetAllReplicasResponse : IReplicaContentResult, IServiceResult
    {
        public bool IsActive { get; set; }
        public RetryReason RetryReason { get; }
    }

    partial class TouchResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class ExistsResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class InsertResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class ReplaceResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class LookupInResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
    partial class MutateInResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class RemoveResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class UnlockResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class UpsertResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class MutationToken
    {
        public static implicit operator Core.MutationToken(MutationToken token) => new Core.MutationToken(
            token.BucketName,
            (short)token.VbucketId,
            (long)token.VbucketUuid,
            (long)token.SeqNo);
    }

    partial class AppendResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class PrependResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class IncrementResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DecrementResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
}
#endregion

#region Collection Management

namespace Couchbase.Protostellar.Admin.Collection.V1
{
    partial class ListCollectionsResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class CreateCollectionResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DeleteCollectionResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class CreateScopeResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DeleteScopeResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
}

#endregion

#region Bucket Management

namespace Couchbase.Protostellar.Admin.Bucket.V1
{
    partial class DeleteBucketResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class CreateBucketResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class UpdateBucketResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class ListBucketsResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
}


#endregion

#region Search Index Management

namespace Couchbase.Protostellar.Admin.Search.V1
{
    partial class GetIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class ListIndexesResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class UpdateIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class CreateIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DeleteIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class GetIndexedDocumentsCountResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class PauseIndexIngestResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class ResumeIndexIngestResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class AllowIndexQueryingResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DisallowIndexQueryingResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class FreezeIndexPlanResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class UnfreezeIndexPlanResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
}


#endregion

#region Query Index Management
namespace Couchbase.Protostellar.Admin.Query.V1
{
    partial class CreatePrimaryIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class CreateIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class GetAllIndexesResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DropIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class DropPrimaryIndexResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }

    partial class BuildDeferredIndexesResponse : IServiceResult
    {
        public RetryReason RetryReason { get; }
    }
}
#endregion


#endif
