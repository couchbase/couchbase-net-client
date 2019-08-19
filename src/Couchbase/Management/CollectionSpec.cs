namespace Couchbase.Management
{
    public class CollectionSpec
    {
        public string Name { get; }
        public string ScopeName { get; }

        public CollectionSpec(string scopeName, string name)
        {
            ScopeName = scopeName;
            Name = name;
        }
    }
}