using System.Collections.Generic;

namespace Couchbase.Query
{
    public sealed class QueryParameter
    {
        internal Dictionary<string, object> NamedParameters { get; } = new Dictionary<string, object>();
        internal List<object> PostionalParameters { get; } = new List<object>();

        public QueryParameter Add(string name, object value)
        {
            NamedParameters.Add(name, value);
            return this;
        }

        public QueryParameter Add(object value)
        {
            PostionalParameters.Add(value);
            return this;
        }
    }
}
