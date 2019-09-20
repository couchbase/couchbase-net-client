namespace Couchbase.Services.KeyValue
{
    public static class ScopeExtensions
    {
        public static ICollection Collection(this IScope scope, string name)
        {
            return scope[name];
        }
    }
}
