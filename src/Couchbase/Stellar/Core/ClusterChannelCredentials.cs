#if NETCOREAPP3_1_OR_GREATER
using System;
using Grpc.Core;

namespace Couchbase.Stellar.Core;

#nullable enable

internal class ClusterChannelCredentials : ChannelCredentials
{
    public ClusterChannelCredentials(ClusterOptions clusterOptions)
    {
        // TODO: handle both TLS and Basic
        var authBytes = System.Text.Encoding.UTF8.GetBytes($"{clusterOptions.UserName}:{clusterOptions.Password}");
        var auth64 = Convert.ToBase64String(authBytes);
        BasicAuthHeader = $"Basic {auth64}";
    }

    internal string? BasicAuthHeader { get; }

    public override void InternalPopulateConfiguration(ChannelCredentialsConfiguratorBase configurator, object state)
    {
        //
    }
}
#endif
