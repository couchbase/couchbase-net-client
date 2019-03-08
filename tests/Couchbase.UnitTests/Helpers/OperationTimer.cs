using System;
using System.Diagnostics;
using System.Threading;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Helpers
{
    /******************************************************************************
  Module: OperationTimer.cs
  Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
  ******************************************************************************/
///////////////////////////////////////////////////////////////////////////////


// ReSharper disable once CheckNamespace
    namespace Wintellect
    {
        /// <summary>
        ///     This class is useful for timing the duration of an algorithm
        /// </summary>
        public sealed class OperationTimer : IDisposable
        {
            private ITestOutputHelper output;
   
// ReSharper disable once InconsistentNaming
            private static int s_NumOperationTimersStarted;

// ReSharper disable once InconsistentNaming
            private Stopwatch m_sw;

// ReSharper disable once InconsistentNaming
// ReSharper disable once FieldCanBeMadeReadOnly.Local
            private string m_text;

// ReSharper disable InconsistentNaming
// ReSharper disable FieldCanBeMadeReadOnly.Local
            private int m_collectionCount;
// ReSharper restore FieldCanBeMadeReadOnly.Local
// ReSharper restore InconsistentNaming

            /// <summary>
            ///     Constructs an OperationTimer with an empty text string
            /// </summary>
            public OperationTimer(ITestOutputHelper output) : this(string.Empty, output)
            {
            }

            /// <summary>
            ///     Constructs an OperationTimer with text identifying the operation
            /// </summary>
            /// <param name="text">Text describing the operation.</param>
            public OperationTimer(string text, ITestOutputHelper output)
            {
                this.output = output;
                if (Interlocked.Increment(ref s_NumOperationTimersStarted) == 1)
                    PrepareForOperation();
                m_text = text;
                m_collectionCount = GC.CollectionCount(0);
                m_sw = Stopwatch.StartNew(); // This should be the last statement in this method
            }

            /// <summary>
            ///     Call this when the operation is done to see how long it took and how many GCs occurred.
            /// </summary>
            public void Dispose()
            {
                output.WriteLine("{0,7:N0} (GCs={1,3}) {2}",
                    m_sw.Elapsed.TotalMilliseconds,
                    GC.CollectionCount(0) - m_collectionCount, m_text);
                Interlocked.Decrement(ref s_NumOperationTimersStarted);
                m_sw = null;
            }

            private static void PrepareForOperation()
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }
}