using Couchbase.ConcurrencyTests.Connections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.ConcurrencyTests.Actors
{
    /// <summary>
    /// A single actor running a simple set of operations in a loop.
    /// </summary>
    internal abstract class Actor : IAsyncDisposable
    {
        static long NextActorId = 0;
        public static long GetActorId() => Interlocked.Increment(ref NextActorId);

        public readonly long ActorId = GetActorId();

        protected readonly CancellationTokenSource internalCancellation = new();

        [Flags]
        public enum RunStatus
        {
            NotStarted = 0,
            Started = 1,
            Finished = 2,
            Faulted = 4,
        }

        public RunStatus Status { get; protected set; }

        /// <summary>
        /// Gets a Counter tied to this concrete implementation for RunCycles.
        /// </summary>
        protected abstract Counter<long> RunCyclesCounter { get; }

        /// <summary>
        /// Gets the name of this actor implementation.
        /// </summary>
        public abstract string ActorName { get; }

        /// <summary>
        /// Increment the count of actors currently running of this type.
        /// </summary>
        /// <returns>The count after increment.</returns>
        protected abstract long IncrementActorsRunning();

        /// <summary>
        /// Decrement the count of actors currently running of this type.
        /// </summary>
        /// <returns>The count after decrement.</returns>
        protected abstract long DecrementActorsRunning();

        /// <summary>
        /// Perform any warmup tasks for this type of actor.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to govern how much time Warmup can take.</param>
        /// <returns>A Task representing asynchronous work.</returns>
        public virtual Task Warmup(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Run the operations in a loop until cancelled.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token controlling how long the Run loop will continue.</param>
        /// <returns>A Task representing the asynchronous work.</returns>
        public async Task Run(CancellationToken cancellationToken)
        {
            Status = RunStatus.Started;
            var actorsRunning = IncrementActorsRunning();
            Serilog.Log.Information("[{aid}] {ActorName} started running.", this.ActorId, this.ActorName);
            var linkedCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.internalCancellation.Token);
            try
            {
                await RunInternal(linkedCancel.Token);
                RunCyclesCounter.Add(1);
            }
            catch (OperationCanceledException)
            {
                // don't throw due to intentional cancel
                if (!linkedCancel.Token.IsCancellationRequested)
                {
                    Status |= RunStatus.Faulted;
                    throw;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException && linkedCancel.IsCancellationRequested))
                {
                    Status |= RunStatus.Faulted;
                    throw;
                }
                else
                {
                    // cancellation due time/Ctrl-C is not an error case.
                }
            }
            finally
            {
                DecrementActorsRunning();
                Status |= RunStatus.Finished;
                Serilog.Log.Information("[{aid}] {ActorName} finished running with status={Status}.", this.ActorId, this.ActorName, this.Status);
            }
        }

        /// <summary>
        /// The internal Run implementation of each actor implementation, not including the boilerplate.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token governing how long the Run loop will continue.</param>
        /// <returns>A Task representing the asynchronous work.</returns>
        protected abstract Task RunInternal(CancellationToken cancellationToken);

        /// <summary>
        /// Perform any cleanup associated with this type of actor.
        /// </summary>
        /// <param name="cancellationToken">A token limiting the time spent in Cleanup.</param>
        /// <returns>A Task representing the asynchronous work.</returns>
        public virtual Task Cleanup(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public virtual async ValueTask DisposeAsync()
        {
            internalCancellation.Cancel();
            await Cleanup(CancellationToken.None);
            internalCancellation.Dispose();
        }
    }
}
