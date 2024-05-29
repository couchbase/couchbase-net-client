#nullable enable
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Stj = System.Text.Json.Serialization;
using NSft = Newtonsoft.Json;

namespace Couchbase.Search.Queries.Vector;

[InterfaceStability(Level.Committed)]
public sealed record VectorQuery(
    [property: Stj.JsonPropertyName(VectorQuery.PropField)]
    [property: NSft.JsonProperty(VectorQuery.PropField)]
    string VectorFieldName,

    [property: Stj.JsonIgnore]
    [property: NSft.JsonIgnore]
    VectorQueryOptions? Options = null)
{

    private float[]? _vector = null;
    private string? _base64Vector = null;

    [Stj.JsonPropertyName(PropVector)]
    [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
    [NSft.JsonProperty(PropVector, NullValueHandling = NSft.NullValueHandling.Ignore)]
    public float[]? Vector
    {
        get => _vector;
        init
        {
            IsVectorQueryValid(value, _base64Vector);
            _vector = value;
        }
    }

    [Stj.JsonPropertyName(PropBase64Vector)]
    [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
    [NSft.JsonProperty(PropBase64Vector, NullValueHandling = NSft.NullValueHandling.Ignore)]
    public string? Base64EncodedVector
    {
        get => _base64Vector;
        init
        {
            IsVectorQueryValid(value, _vector);
            _base64Vector = value;
        }
    }

    private static void IsVectorQueryValid(object? incomingValue, object? otherProperty)
    {
        if (incomingValue == null || otherProperty != null)
        {
            throw new InvalidArgumentException("A vector has to provided either as a float[] or a Base64-encoded string, but not both.");
        }
    }

    public static VectorQuery Create(string vectorFieldName, float[] vector, VectorQueryOptions? options = null) =>
        new VectorQuery(vectorFieldName, options) {Vector = vector};

    public static VectorQuery Create(string vectorFieldName, string base64EncodedVector, VectorQueryOptions? options = null) =>
        new VectorQuery(vectorFieldName, options) {Base64EncodedVector = base64EncodedVector};

    public const int DefaultNumCandidates = 3;
    private const string PropField = "field";
    private const string PropVector = "vector";
    private const string PropBase64Vector = "vector_base64";
    private const string PropNumCandidates = "k";
    private const string PropBoost = "boost";

    [Stj.JsonPropertyName(PropNumCandidates)]
    [NSft.JsonProperty(PropNumCandidates)]
    public uint NumCandidates => Options?.NumCandidates ?? DefaultNumCandidates;

    [Stj.JsonPropertyName(PropBoost)]
    [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
    [NSft.JsonProperty(PropBoost, NullValueHandling = NSft.NullValueHandling.Ignore)]
    public float? Boost => Options?.Boost;

    public VectorQuery WithVector(float[] vector) => this with { Vector = vector };
    public VectorQuery WithBase64EncodedVector(string vector) => this with { Base64EncodedVector = vector };

    public VectorQuery WithOptions(VectorQueryOptions options) => this with { Options = options };
}

[InterfaceStability(Level.Committed)]
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
