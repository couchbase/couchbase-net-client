using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Analytics
{
    public interface IAnalyticsClient
    {
        /// <summary>
        /// Executes an Analytics request against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="request">The <see cref="IAnalyticsRequest"/> to execute.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        IAnalyticsResult<T> Query<T>(IAnalyticsRequest request);

        /// <summary>
        /// Asynchronously executes an analytics request against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="request">The <see cref="IAnalyticsRequest"/> to execute.</param>
        /// <param name="token">A cancellation token that can be used to cancel the request.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest request, CancellationToken token);
    }
}
