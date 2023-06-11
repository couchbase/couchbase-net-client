using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Management.Eventing.Internal;

namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// The manager allows the user to read functions, modify them and change their deployment state.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public interface IEventingFunctionManager
    {
        Task UpsertFunctionAsync(EventingFunction function, UpsertFunctionOptions options = null);

        /// <summary>
        /// Drops a function.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task DropFunctionAsync(string name, DropFunctionOptions options = null);

        /// <summary>
        /// Deploys a function (from state undeployed to deployed).
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task DeployFunctionAsync(string name, DeployFunctionOptions options = null);

        /// <summary>
        /// Lists all functions (both deployed and undeployed).
        /// </summary>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>An <see cref="IEnumerable{EventFunction}"/> for enumeration of the results.</returns>
        Task<IEnumerable<EventingFunction>> GetAllFunctionsAsync(GetAllFunctionOptions options = null);

        /// <summary>
        /// Fetches a specific function.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns></returns>
        Task<EventingFunction> GetFunctionAsync(string name, GetFunctionOptions options = null);

        /// <summary>
        /// Pauses a function.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task PauseFunctionAsync(string name, PauseFunctionOptions options = default);

        /// <summary>
        /// Resumes a function if it is paused.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task ResumeFunctionAsync(string name, ResumeFunctionOptions options = default);

        /// <summary>
        /// Undeploys a function (from state deployed to undeployed).
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task UndeployFunctionAsync(string name, UndeployFunctionOptions options = default);

        /// <summary>
        /// Receives the status of all the eventing functions.
        /// </summary>
        /// <param name="options">Any optional parameters.</param>
        /// <returns></returns>
        Task<EventingStatus> FunctionsStatus(FunctionsStatusOptions options = default);
    }

    public class EventingStatus
    {
        [JsonPropertyName("num_eventing_nodes")]
        public int NumEventingNodes { get; set; }

        [JsonPropertyName("apps")]
        public List<EventingFunctionState> Functions { get; set; }
    }

    public class EventingFunctionState
    {
        public string Name { get; set; }

        [JsonPropertyName("composite_status")]
        [JsonConverter(typeof(EventingFunctionStatusConverter))]
        public EventingFunctionStatus Status { get; set; }

        [JsonPropertyName("num_bootstrapping_nodes")]
        public int NumBootstrappingNodes { get; set; }

        [JsonPropertyName("num_deployed_nodes")]
        public int NumDeployedNodes { get; set; }

        [JsonPropertyName("deployment_status")]
        [JsonConverter(typeof(EventingFunctionDeploymentStatusConverter))]
        public EventingFunctionDeploymentStatus DeploymentStatus { get; set; }

        [JsonPropertyName("processing_status")]
        [JsonConverter(typeof(EventingFunctionProcessingStatusConverter))]
        public EventingFunctionProcessingStatus ProcessingStatus { get; set; }
    }

    public enum EventingFunctionStatus
    {
        Undeployed,
        Deploying,
        Deployed,
        UnDeploying,
        Paused,
        Pausing
    }


    /// <summary>
    /// Base class for event function options.
    /// </summary>
    public abstract class FunctionOptionsBase
    {
        /// <summary>
        /// A <see cref="TimeSpan"/> representing the timeout duration.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(75000);

        /// <summary>
        /// An optional <see cref="CancellationToken"/> which will use the default timeout if null.
        /// </summary>
        public CancellationToken Token { get; set; }

        /// <summary>
        /// An optional parent <see cref="IRequestSpan"/> for tracing.
        /// </summary>
        public IRequestSpan RequestSpan { get; set; }

        /// <summary>
        /// The unique client context id of request.
        /// </summary>
        public string ClientContextId { get; set; } = Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public class UndeployFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static UndeployFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class ResumeFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static ResumeFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class PauseFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static PauseFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class GetAllFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static GetAllFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class GetFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static GetFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class DropFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static DropFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class DeployFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static DeployFunctionOptions Default { get; } = new();
    }

    /// <inheritdoc />
    public class UpsertFunctionOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static UpsertFunctionOptions Default { get; } = new();
    }

    public class FunctionsStatusOptions : FunctionOptionsBase
    {
        /// <summary>
        /// Gets a default instance of the options class.
        /// </summary>
        internal static FunctionsStatusOptions Default { get; set; } = new();
    }
}
