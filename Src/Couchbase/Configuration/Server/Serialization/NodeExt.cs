using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    /// <summary>
    /// Represents the nodesExt element of a server configuration; the
    /// extended set of services that a node is configured to have (data, query, index, etc)
    /// </summary>
    public sealed class NodeExt
    {
	    private const string DefaultHostname = "127.0.0.1";

		public NodeExt()
        {
            Services = new Services();
	        Hostname = DefaultHostname;
        }

        /// <summary>
        /// Gets or sets the services that this node has available.
        /// </summary>
        /// <value>
        /// The services.
        /// </value>
        [JsonProperty("services")]
        public Services Services { get; set; }

        /// <summary>
        /// Gets or sets the hostname or IP address of this node.
        /// </summary>
        /// <value>
        /// The hostname.
        /// </value>
        [JsonProperty("hostname")]
        public string Hostname { get; set; }
    }
}
