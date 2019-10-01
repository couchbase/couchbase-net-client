namespace Couchbase.Management.Analytics
{
    public class AnalyticsIndex
    {
        public string Name { get; set; }
        public string DatasetName { get; set; }
        public string DataverseName { get; set; }
        public bool IsPrimary { get; set; }
    }
}
