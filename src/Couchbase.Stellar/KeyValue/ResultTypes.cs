using System.Text;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.KeyValue;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using LookupInRequest = Couchbase.Protostellar.KV.V1.LookupInRequest;
using MutateInRequest = Couchbase.Protostellar.KV.V1.MutateInRequest;
using MutationToken = Couchbase.Core.MutationToken;

namespace Couchbase.Stellar.KeyValue
{
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
            return spec.Status == null;
        }

        public T? ContentAs<T>(int index)
        {
            var spec = SpecOrInvalid(index);
            var contentWrapper = new GrpcContentWrapper(spec.Content, 0, this.Serializer);
            return contentWrapper.ContentAs<T>();
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
    }

    internal interface IContentResult
    {
        Timestamp Expiry { get; }
        ByteString Content { get; }
        uint ContentFlags { get; }
        ulong Cas { get; }
    }
}

namespace Couchbase.Protostellar.KV.V1
{
    public partial class GetResponse : IContentResult { }
    public partial class GetAndLockResponse : IContentResult { }
    public partial class GetAndTouchResponse : IContentResult { }

    partial class MutationToken
    {
        public static implicit operator Core.MutationToken(MutationToken token) => new Core.MutationToken(
            token.BucketName,
            (short)token.VbucketId,
            (long)token.VbucketUuid,
            (long)token.SeqNo);
    }
}
