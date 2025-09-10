#nullable enable
using Couchbase.Core.Compatibility;
using Couchbase.Utils;
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
            if (value!.Length == 0)
            {
                ThrowHelper.ThrowInvalidArgumentException("The provided Vector cannot be an empty float array.");
            }
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
            if (string.IsNullOrEmpty(value))
            {
                ThrowHelper.ThrowInvalidArgumentException("The Base64-encoded vector cannot be an empty string.");
            }
            _base64Vector = value;
        }
    }

    private static void IsVectorQueryValid(object? incomingValue, object? otherProperty)
    {
        if (incomingValue == null || otherProperty != null)
        {
            ThrowHelper.ThrowInvalidArgumentException("A vector has to provided either as a float[] or a Base64-encoded string, but not both.");
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
    private const string PropFilter = "filter";

    [Stj.JsonPropertyName(PropNumCandidates)]
    [NSft.JsonProperty(PropNumCandidates)]
    public uint NumCandidates => Options?.NumCandidates ?? DefaultNumCandidates;

    [Stj.JsonPropertyName(PropBoost)]
    [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
    [NSft.JsonProperty(PropBoost, NullValueHandling = NSft.NullValueHandling.Ignore)]
    public float? Boost => Options?.Boost;

    [Stj.JsonPropertyName(PropFilter)]
    [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
    [NSft.JsonProperty(PropFilter, NullValueHandling = NSft.NullValueHandling.Ignore)]
    [Stj.JsonConverter(typeof(Serialization.SearchQuerySystemTextJsonConverter))]
    [NSft.JsonConverter(typeof(Serialization.SearchQueryNewtonsoftConverter))]
    public ISearchQuery? Filter => Options?.Filter;

    public VectorQuery WithVector(float[] vector) => this with { Vector = vector };
    public VectorQuery WithBase64EncodedVector(string vector) => this with { Base64EncodedVector = vector };

    public VectorQuery WithOptions(VectorQueryOptions options) => this with { Options = options };
}

[InterfaceStability(Level.Committed)]
public sealed record VectorQueryOptions(float? Boost = null, ISearchQuery? Filter = null)
{
    private readonly uint? _numCandidates = null;

    public uint? NumCandidates
    {
        get => _numCandidates;
        init
        {
            if (value is < 1)
            {
                ThrowHelper.ThrowInvalidArgumentException($"{nameof(NumCandidates)} must be >= 1");
            }

            _numCandidates = value;
        }
    }
    public VectorQueryOptions WithNumCandidates(uint numCandidates) => this with { NumCandidates = numCandidates };
    public VectorQueryOptions WithBoost(float boost) => this with { Boost = boost };
    public VectorQueryOptions WithFilter(ISearchQuery filter) => this with { Filter = filter };
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
