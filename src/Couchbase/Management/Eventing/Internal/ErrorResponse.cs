using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class Info
    {
        [JsonProperty("area")]
        public string Area { get; set; }

        [JsonProperty("column_number")]
        public int ColumnNumber { get; set; }

        [JsonProperty("compile_success")]
        public bool CompileSuccess { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("line_number")]
        public int LineNumber { get; set; }
    }

    internal class RuntimeInfo
    {
        [JsonProperty("code")]
        public int Code { get; set; }
        [JsonProperty("info")]
        public dynamic Info { get; set; } //Server API can either return a string or an object here
    }

    internal class ErrorResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("description")]
        public dynamic Description { get; set; }

        [JsonProperty("attributes")]
        public object Attributes { get; set; }

        [JsonProperty("runtime_info")]
        public RuntimeInfo RuntimeInfo { get; set; }

        /// <summary>
        /// Gets the decription of the failure handling the case where
        /// the server returns either a string or an object that contains
        /// the description. If an exception is thrown other than a
        /// RuntimeBinderException is thrown the higher level error handling
        /// handle it into a System.Exception.
        /// </summary>
        /// <returns>The description of the failure.</returns>
        public string GetDescription()
        {
            try
            {
                return Description;
            }
            catch (RuntimeBinderException)
            {
                return Description.description.Value;
            }
        }
    }
}
