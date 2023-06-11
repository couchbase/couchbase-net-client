using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class Info
    {
        public string? Area { get; set; }

        [JsonPropertyName("column_number")]
        public int ColumnNumber { get; set; }

        [JsonPropertyName("compile_success")]
        public bool CompileSuccess { get; set; }

        public string? Description { get; set; }

        public int Index { get; set; }

        public string? Language { get; set; }

        [JsonPropertyName("line_number")]
        public int LineNumber { get; set; }
    }

    internal class RuntimeInfo
    {
        public int Code { get; set; }

        public JsonElement Info { get; set; } //Server API can either return a string or an object here
    }

    internal class ErrorResponse
    {
        public string? Name { get; set; }

        public int Code { get; set; }

        public JsonElement Description { get; set; }

        public JsonElement Attributes { get; set; }

        [JsonPropertyName("runtime_info")]
        public RuntimeInfo? RuntimeInfo { get; set; }

        /// <summary>
        /// Gets the description of the failure handling the case where
        /// the server returns either a string or an object that contains
        /// the description.
        /// </summary>
        /// <returns>The description of the failure or null if not found.</returns>
        public string? GetDescription()
        {
            switch (Description)
            {
                case { ValueKind: JsonValueKind.String }:
                    return Description.GetString();

                case {ValueKind: JsonValueKind.Object}:
                    if (Description.TryGetProperty("description", out var description))
                    {
                        return description switch
                        {
                            {ValueKind: JsonValueKind.String} => description.GetString(),
                            _ => null
                        };
                    }
                    break;
            }

            return null;
        }
    }
}
