namespace Couchbase.IntegrationTests.Fixtures
{
    public class TestSettings
    {
        public string ConnectionString { get; set; }
        public string BucketName { get; set; }
        public bool EnableLogging { get; set; }
        public bool SystemTextJson { get; set; }
        public string CertificatesFilePath { get; set; }
        public bool EnableCompression { get; set; }
    }
}
