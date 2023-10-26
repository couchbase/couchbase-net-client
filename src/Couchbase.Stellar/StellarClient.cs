using Couchbase.Stellar.CouchbaseClient;

namespace Couchbase.Stellar;

public class StellarClient
{
    public static async Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? clusterOptions = null)
    {
        clusterOptions ??= new ClusterOptions();

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var parsedUri))
        {
            throw new ArgumentOutOfRangeException(nameof(connectionString));
        }

        var scheme = parsedUri.Scheme.ToLowerInvariant();
        switch (scheme)
        {
            case "protostellar":
            case "protostellars": //TODO: Change to couchbase2 ?
            {
                // hack to get around pre-existing validation of Uri scheme.
                var modifiedUri = new UriBuilder(parsedUri);
                modifiedUri.Scheme = scheme.EndsWith("s") ? "couchbases" : "couchbase";
                clusterOptions.ConnectionString = modifiedUri.Uri.ToString().TrimEnd('/');
                var clusterWrapper = new ProtoCluster(clusterOptions);
                await clusterWrapper.ConnectGrpcAsync(CancellationToken.None).ConfigureAwait(false);
                return clusterWrapper;
            }
        }

        clusterOptions.ConnectionString = connectionString;
        var classicCluster = await Cluster.ConnectAsync(connectionString, clusterOptions).ConfigureAwait(false);
        return classicCluster;

    }
}