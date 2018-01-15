using OpenTracing;

namespace Couchbase.Tracing
{
    internal class Reference
    {
        public string Type { get; }
        public ISpanContext Context { get; }

        public Reference(string type, ISpanContext context)
        {
            Type = type;
            Context = context;
        }
    }
}
