using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Provides functionality for tracing keys within the Couchbase client and IO layers
    /// </summary>
    public static class TraceSourceExtensions
    {
        private static int _messageCount = 0;

        /// <summary>
        /// Writes a Trace message for a given key
        /// </summary>
        /// <param name="traceSource">The TraceSource context to trace against</param>
        /// <param name="key">The message key to trace</param>
        public static void TraceKey(this TraceSource traceSource, string key)
        {
            //If no tracing is enabled there is no need to continue
            if (traceSource.Switch.Level == SourceLevels.Off) return;

            //For now, just provide a simple output of key and method name that it was traced in
            var message = string.Format("Tracing key [{0}] in method [{1}]", key, GetMethodName());
            TraceKey(traceSource, key, message);
        }

        /// <summary>
        /// Writes a Trace message for a given key - allows a more specific message to be written
        /// </summary>
        /// <param name="traceSource">The TraceSource context to trace against</param>
        /// <param name="key">The message key to trace</param>
        /// <param name="message">A context sensitive trace message</param>
        public static void TraceKey(this TraceSource traceSource, string key, string message)
        {
            //Used to count # of trace events - will return 0 on overflow
            var count = Interlocked.Increment(ref _messageCount);

            //Only trace Information events for keys
            traceSource.TraceData(TraceEventType.Information, count, key, message);
        }

        /// <summary>
        /// Gets the name of the method that called TraceKey
        /// </summary>
        /// <returns>The name of the method that called TraceKey</returns>
        static string GetMethodName()
        {
            //The caller must be three frames back: GetMethodName()=>TraceKey()=>Caller. Fragile.
            const int threeFramesDeep = 2;
            var methodName = string.Empty;

            var stackTrace = new StackTrace();
            if (stackTrace.FrameCount >= threeFramesDeep)
            {
                var stackFrame = stackTrace.GetFrame(threeFramesDeep);
                var methodBase = stackFrame.GetMethod();
                methodName = methodBase.Name;
            }
            return methodName;
        }
    }
}
