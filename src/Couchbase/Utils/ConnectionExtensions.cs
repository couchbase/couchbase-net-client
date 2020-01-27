using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SequenceGenerator = Couchbase.Core.IO.Operations.SequenceGenerator;

namespace Couchbase.Utils
{
    internal static class ConnectionExtensions
    {
        internal static async Task<uint?> GetCid(this IConnection connection, string name, ITypeTranscoder transcoder)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using var getCid = new GetCid
            {
                Key = name,
                Transcoder = transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Content = null,
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            };
            await getCid.SendAsync(connection).ConfigureAwait(false);
            var gitCidBytes = await completionSource.Task.ConfigureAwait(false);
            await getCid.ReadAsync(gitCidBytes).ConfigureAwait(false);

            var resultWithValue = getCid.GetResultWithValue();
            return resultWithValue.Content;
        }

        internal static async Task<Manifest> GetManifest(this IConnection connection, ITypeTranscoder transcoder)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using var manifestOp = new GetManifest
            {
                Transcoder = transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            };
            await manifestOp.SendAsync(connection).ConfigureAwait(false);
            var manifestBytes = await completionSource.Task.ConfigureAwait(false);
            await manifestOp.ReadAsync(manifestBytes).ConfigureAwait(false);

            var manifestResult = manifestOp.GetResultWithValue();
            return manifestResult.Content;
        }

        internal static async Task<short[]> Hello(this IConnection connection, ITypeTranscoder transcoder)
        {
            //TODO missing MutationSeqno (0x04) and ServerDuration (0x0f)
            var features = new List<short>
            {
                (short) ServerFeatures.SelectBucket,
                (short) ServerFeatures.Collections,
                (short) ServerFeatures.AlternateRequestSupport,
                (short) ServerFeatures.SynchronousReplication,
                (short) ServerFeatures.SubdocXAttributes,
                (short) ServerFeatures.XError
            };
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using var heloOp = new Hello
            {
                Key = Core.IO.Operations.Hello.BuildHelloKey(connection.ConnectionId),
                Content = features.ToArray(),
                Transcoder = transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            };
            await heloOp.SendAsync(connection).ConfigureAwait(false);
            var result = await completionSource.Task.ConfigureAwait(false);
            await heloOp.ReadAsync(result).ConfigureAwait(false);

            //returns all supported features
            return heloOp.GetResultWithValue().Content;
        }

        public static async Task Authenticate(this IConnection connection, ClusterOptions clusterOptions,
            string bucketName, CancellationToken cancellationToken = default)
        {
            var sasl = new PlainSaslMechanism(clusterOptions.UserName, clusterOptions.Password);
            var authenticated = await sasl.AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false);
            if (!authenticated)
            {
                throw new AuthenticationFailureException($"Cannot authenticate {bucketName}");
            }
        }

        public static async Task SelectBucket(this IConnection connection, string bucketName, ITypeTranscoder transcoder)
        {
            var completionSource = new TaskCompletionSource<bool>();
            using var selectBucketOp = new SelectBucket
            {
                Transcoder = transcoder,
                Key = bucketName,
                Completed = s =>
                {
                    completionSource.TrySetResult(s.Status == ResponseStatus.Success);
                    return completionSource.Task;
                }
            };
            await selectBucketOp.SendAsync(connection).ConfigureAwait(false);
        }

        public static async Task<ErrorMap> GetErrorMap(this IConnection connection, ITypeTranscoder transcoder)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using var errorMapOp = new GetErrorMap
            {
                Transcoder = transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            };
            await errorMapOp.SendAsync(connection).ConfigureAwait(false);
            var result = await completionSource.Task.ConfigureAwait(false);
            await errorMapOp.ReadAsync(result).ConfigureAwait(false);

            return errorMapOp.GetResultWithValue().Content;
        }
    }
}
