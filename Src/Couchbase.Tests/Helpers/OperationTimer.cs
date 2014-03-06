/******************************************************************************
Module: OperationTimer.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect
{
    /// <summary>
    /// This class is useful for timing the duration of an algorithm
    /// </summary>
    public sealed class OperationTimer : IDisposable
    {
        private static Int32 s_NumOperationTimersStarted;
        private Stopwatch m_sw;
        private String m_text;
        private Int32 m_collectionCount;

        /// <summary>
        /// Constructs an OperationTimer with an empty text string
        /// </summary>
        public OperationTimer() : this(String.Empty) { }

        /// <summary>
        /// Constructs an OperationTimer with text identifying the operation
        /// </summary>
        /// <param name="text">Text describing the operation.</param>
        public OperationTimer(String text)
        {
            if (Interlocked.Increment(ref s_NumOperationTimersStarted) == 1)
                PrepareForOperation();
            m_text = text;
            m_collectionCount = GC.CollectionCount(0);
            m_sw = Stopwatch.StartNew();	// This should be the last statement in this method
        }

        /// <summary>
        /// Call this when the operation is done to see how long it took and how many GCs occurred.
        /// </summary>
        public void Dispose()
        {
            Console.WriteLine("{0,7:N0} (GCs={1,3}) {2}",
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