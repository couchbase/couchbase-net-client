#nullable enable
using Couchbase.Core.Compatibility;
using Stj = System.Text.Json.Serialization;
using NSft = Newtonsoft.Json;

namespace Couchbase.Search.Queries.Vector;

[InterfaceStability(Level.Volatile)]
public sealed record VectorQuery(
    [property: Stj.JsonPropertyName(VectorQuery.PropField)]
    [property: NSft.JsonProperty(VectorQuery.PropField)]
    string VectorFieldName,

    [property: Stj.JsonPropertyName(VectorQuery.PropVector)]
    [property: NSft.JsonProperty(VectorQuery.PropVector)]
    float[] Vector,

    [property: Stj.JsonIgnore]
    [property: NSft.JsonIgnore]
    VectorQueryOptions? Options = null)
{
    public const int DefaultNumCandidates = 3;
    private const string PropField = "field";
    private const string PropVector = "vector";
    private const string PropNumCandidates = "k";
    private const string PropBoost = "boost";

    [Stj.JsonPropertyName(PropNumCandidates)]
    [NSft.JsonProperty(PropNumCandidates)]
    public uint NumCandidates => Options?.NumCandidates ?? DefaultNumCandidates;

    [Stj.JsonPropertyName(PropBoost)]
    [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
    [NSft.JsonProperty(PropBoost, NullValueHandling = NSft.NullValueHandling.Ignore)]
    public float? Boost => Options?.Boost;

    public VectorQuery WithOptions(VectorQueryOptions options) => this with { Options = options };
}

[InterfaceStability(Level.Volatile)]
public sealed record VectorQueryOptions(float? Boost = null)
{
    private readonly uint? _numCandidates = null;

    public uint? NumCandidates
    {
        get => _numCandidates;
        init
        {
            if (value is < 1)
            {
                throw new Core.Exceptions.InvalidArgumentException($"{nameof(NumCandidates)} must be >= 1");
            }

            _numCandidates = value;
        }
    }
    public VectorQueryOptions WithNumCandidates(uint numCandidates) => this with { NumCandidates = numCandidates };
    public VectorQueryOptions WithBoost(float boost) => this with { Boost = boost };
}
