using Newtonsoft.Json;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents a single row returned from a View request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ViewRow<T>
    {
        /// <summary>
        /// The identifier for the row
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// The key emitted by the View Map function
        /// </summary>
        [JsonProperty("key")]
        public object Key { get; set; }

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce
        /// </summary>
        [JsonProperty("value")]
        public T Value { get; set; }
    }
}
