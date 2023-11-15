using Couchbase.Stellar;

// ReSharper disable once CheckNamespace
namespace Couchbase;

public static class Cluster
{
    public static Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? clusterOptions = null)
    {
        var options = clusterOptions ?? new ClusterOptions();
        options.WithConnectionString(connectionString);
        return ConnectAsync(options);
    }

    public static Task<ICluster> ConnectAsync(string connectionString, Action<ClusterOptions> configureOptions)
    {
        var options = new ClusterOptions();
        configureOptions.Invoke(options);
        return ConnectAsync(connectionString, options);
    }

    public static Task<ICluster> ConnectAsync(string connectionString, string username, string password)
    {
        return ConnectAsync(connectionString, new ClusterOptions
        {
            UserName = username,
            Password = password
        });
    }

    public static Task<ICluster> ConnectAsync(ClusterOptions options)
    {
        var connectionString = options.ConnectionStringValue ?? throw new ArgumentNullException(nameof(options));
        var schema = connectionString.Scheme;
        return schema switch
        {
            Scheme.Couchbase => NetClient.Cluster.ConnectAsync(options),
            Scheme.Couchbases => NetClient.Cluster.ConnectAsync(options),
            Scheme.Couchbase2 => StellarCluster.ConnectAsync(options),
            _ => throw new NotImplementedException()
        };
    }
}
