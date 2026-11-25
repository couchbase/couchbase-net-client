#if NETCOREAPP3_1_OR_GREATER
using System.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;

namespace Couchbase.Stellar.Core;

#nullable enable

internal static class StreamSpan
{
    /// <summary>
    /// Ends a span that a streamed result owns, and keeps the caller's <see cref="Activity.Current"/>.
    /// </summary>
    /// <remarks>
    /// A streamed result can end its span after the caller has started other activities, for example
    /// when the caller disposes the result. <see cref="Activity.Stop"/> sets <see cref="Activity.Current"/>
    /// to the activity that was current when the stopped activity started, even if the stopped activity
    /// is not current. Without this, the caller's own activity would be lost.
    /// </remarks>
    public static void EndKeepingCurrentActivity(IRequestSpan span)
    {
        var current = Activity.Current;
        span.End();
        if (Activity.Current != current)
        {
            Activity.Current = current;
        }
    }
}
#endif
