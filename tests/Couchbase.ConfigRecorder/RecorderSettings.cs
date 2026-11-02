using System.Text.Json;
using System.Text.Json.Nodes;

namespace Couchbase.ConfigRecorder;

internal sealed class LoadSettings
{
    public int Workers { get; set; } = 64;
    public int KeyCount { get; set; } = 10000;
    public int ValueSize { get; set; } = 256;
}

internal sealed class RecorderSettings
{
    public string ConnectionString { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string OutputFolder { get; set; } = "runs";
    public int ProbeIntervalMs { get; set; } = 500;
    public int SdkSampleIntervalMs { get; set; } = 1000;
    public int DnsSrvIntervalMs { get; set; } = 10000;
    public LoadSettings Load { get; set; } = new();

    /// <summary>
    /// Accepts any server certificate. Only for local test clusters.
    /// </summary>
    public bool AcceptAnyServerCertificate { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static RecorderSettings FromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new SettingsException($"Settings file '{path}' does not exist. Copy recorder.example.json to recorder.json and edit it.");
        }

        RecorderSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<RecorderSettings>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException e)
        {
            throw new SettingsException($"Settings file '{path}' is not valid JSON. {e.Message}");
        }

        if (settings is null)
        {
            throw new SettingsException($"Settings file '{path}' is empty.");
        }

        settings.Validate();
        return settings;
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ConnectionString)) errors.Add("connectionString is required.");
        if (string.IsNullOrWhiteSpace(Username)) errors.Add("username is required.");
        if (string.IsNullOrEmpty(Password)) errors.Add("password is required.");
        if (string.IsNullOrWhiteSpace(Bucket)) errors.Add("bucket is required.");
        if (string.IsNullOrWhiteSpace(OutputFolder)) errors.Add("outputFolder is required.");
        if (ProbeIntervalMs < 50) errors.Add("probeIntervalMs must be 50 or more.");
        if (SdkSampleIntervalMs < 50) errors.Add("sdkSampleIntervalMs must be 50 or more.");
        if (DnsSrvIntervalMs < 1000) errors.Add("dnsSrvIntervalMs must be 1000 or more.");
        if (Load.Workers < 0) errors.Add("load.workers must be 0 or more.");
        if (Load.KeyCount < 1) errors.Add("load.keyCount must be 1 or more.");
        if (Load.ValueSize < 0) errors.Add("load.valueSize must be 0 or more.");
        if (errors.Count > 0)
        {
            throw new SettingsException("Invalid settings. " + string.Join(" ", errors));
        }
    }

    /// <summary>
    /// The settings without the password, for the run record.
    /// </summary>
    public JsonObject ToSafeJson() => new()
    {
        ["connectionString"] = ConnectionString,
        ["username"] = Username,
        ["bucket"] = Bucket,
        ["outputFolder"] = OutputFolder,
        ["probeIntervalMs"] = ProbeIntervalMs,
        ["sdkSampleIntervalMs"] = SdkSampleIntervalMs,
        ["dnsSrvIntervalMs"] = DnsSrvIntervalMs,
        ["load"] = new JsonObject
        {
            ["workers"] = Load.Workers,
            ["keyCount"] = Load.KeyCount,
            ["valueSize"] = Load.ValueSize,
        },
        ["acceptAnyServerCertificate"] = AcceptAnyServerCertificate,
    };
}

internal sealed class SettingsException(string message) : Exception(message);
