using OpenTracing;

namespace Couchbase.Tracing
{
    internal static class SpanBuilderExtensions
    {
        internal static ISpanBuilder WithIgnoreTag(this ISpanBuilder builder)
        {
            return builder.WithTag(CouchbaseTags.Ignore, true);
        }
    }
}
