namespace Couchbase.IntegrationTests.Utils
{
    public class TestSettings
    {
        public string Current { get; set; }
        public string Hostname { get; set; }
        public string BootPort { get; set; }
        public string AdminUsername { get; set; }
        public string AdminPassword { get; set; }
        public bool EnhancedAuth { get; set; }
        public bool IsMock { get; set; }
    }
}
