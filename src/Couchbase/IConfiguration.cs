using System;
using System.Collections.Generic;

namespace Couchbase
{
    public interface IConfiguration
    {
        IConfiguration WithServers(params string[] ips);

        IConfiguration WithBucket(params string[] bucketNames);

        IConfiguration WithCredentials(string username, string password);

        IEnumerable<Uri> Servers { get; }

        IEnumerable<string> Buckets { get; }

        string UserName { get; set; }

        string Password { get; set; }


        TimeSpan ConnectTimeout { get; set; }
        TimeSpan KvTimeout { get; set; }
        TimeSpan ViewTimeout { get; set; }
        TimeSpan QueryTimeout { get; set; }
        TimeSpan AnalyticsTimeout { get; set; }
        TimeSpan SearchTimeout { get; set; }
        TimeSpan ManagementTimeout { get; set; }
        bool UseSsl { get; set; }
        bool EnableTracing { get; set; }
        bool EnableMutationTokens { get; set; }

        #region Dotnet Specific
        bool Expect100Continue { get; set; }
        bool EnableCertificateAuthentication { get; set; }
        bool EnableCertificateRevocation { get; set; }
        bool IgnoreRemoteCertificateNameMismatch { get; set; }
        int MaxQueryConnectionsPerServer { get; set; }
        #endregion
    }
}
