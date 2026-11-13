using System;
using System.Text.Json.Nodes;
using Couchbase.Core.Exceptions;
using Couchbase.Query.Conversational;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational;

public class ModelParametersTests
{
    private const string Password = "s3cret-pa55";
    private const string Key = "sk-test-key";
    private const string RawSecret = "raw-header-value";

    [Fact]
    public void Capella_MapsToCredAndOrgId()
    {
        var parameters = ModelParameters.Create(
            new ModelOptions().Provider("openai").Name("gpt-4o"),
            ModelCredential.Capella("user@example.com", Password, "org-1"));

        Assert.Equal("user@example.com:" + Password, parameters.Cred);
        Assert.Equal("org-1", parameters.OrgId);
        Assert.Equal("openai", parameters.Vendor);
        Assert.Equal("gpt-4o", parameters.Model);
        Assert.Null(parameters.Config);
    }

    [Fact]
    public void Capella_WithoutModel_LeavesVendorAndModelUnset()
    {
        var parameters = ModelParameters.Create(null, ModelCredential.Capella("user@example.com", Password, "org-1"));

        Assert.Null(parameters.Vendor);
        Assert.Null(parameters.Model);
        Assert.Null(parameters.Config);
    }

    [Fact]
    public void Capella_UnsetOptions_AreAllowed()
    {
        var model = new ModelOptions().Endpoint("").OutputTokenLimit(0);

        var parameters = ModelParameters.Create(model, ModelCredential.Capella("user@example.com", Password, "org-1"));

        Assert.Null(parameters.Config);
    }

    public static TheoryData<Func<ModelOptions, ModelOptions>> CapellaUnsupported => new()
    {
        m => m.Endpoint("https://models.example.com"),
        m => m.OutputTokenLimit(100),
        m => m.Moderation(false),
        m => m.Raw("temperature", 0.2)
    };

    [Theory]
    [MemberData(nameof(CapellaUnsupported))]
    public void Capella_UnsupportedOption_Throws(Func<ModelOptions, ModelOptions> configure)
    {
        var model = configure(new ModelOptions().Provider("openai"));

        Assert.Throws<InvalidArgumentException>(() =>
            ModelParameters.Create(model, ModelCredential.Capella("user@example.com", Password, "org-1")));
    }

    [Fact]
    public void ApiKey_PutsEverythingInConfig()
    {
        var model = new ModelOptions()
            .Provider("openai")
            .Name("gpt-4o")
            .Endpoint("https://models.example.com")
            .OutputTokenLimit(512)
            .Moderation(true)
            .Raw("temperature", 0.2)
            .Raw("headers", new JsonObject { ["x-team"] = "query" });

        var parameters = ModelParameters.Create(model, ModelCredential.ApiKey(Key));

        AssertConfig(
            "{\"provider\":\"openai\",\"model\":\"gpt-4o\",\"endpoint\":\"https://models.example.com\"," +
            "\"output_token_limit\":512,\"moderation\":true,\"api_key\":\"" + Key + "\"," +
            "\"temperature\":0.2,\"headers\":{\"x-team\":\"query\"}}",
            parameters);
        Assert.Null(parameters.Cred);
        Assert.Null(parameters.OrgId);
        Assert.Null(parameters.Vendor);
        Assert.Null(parameters.Model);
    }

    [Fact]
    public void CredentialStore_SetsCredId()
    {
        var parameters = ModelParameters.Create(new ModelOptions().Provider("bedrock"),
            ModelCredential.CredentialStore("my-cred"));

        AssertConfig("{\"provider\":\"bedrock\",\"cred_id\":\"my-cred\"}", parameters);
    }

    [Fact]
    public void NoCredential_UsesConfig()
    {
        var parameters = ModelParameters.Create(new ModelOptions().Name("llama3"), null);

        AssertConfig("{\"model\":\"llama3\"}", parameters);
        Assert.Null(parameters.Cred);
    }

    [Fact]
    public void NoModelAndNoCredential_ConfigIsNull()
    {
        var parameters = ModelParameters.Create(null, null);

        Assert.Null(parameters.Config);
        Assert.Null(parameters.Cred);
        Assert.Null(parameters.OrgId);
        Assert.Null(parameters.Vendor);
        Assert.Null(parameters.Model);
    }

    [Fact]
    public void OnlyUnsetOptions_ConfigIsNull()
    {
        var model = new ModelOptions().Provider("").Name("").Endpoint("").OutputTokenLimit(0);

        Assert.Null(ModelParameters.Create(model, null).Config);
    }

    [Fact]
    public void TypedOption_WinsOverRaw()
    {
        var before = new ModelOptions().Raw("provider", "raw").Provider("openai");
        var after = new ModelOptions().Provider("openai").Raw("provider", "raw");

        AssertConfig("{\"provider\":\"openai\"}", ModelParameters.Create(before, null));
        AssertConfig("{\"provider\":\"openai\"}", ModelParameters.Create(after, null));
    }

    [Fact]
    public void UnsetTypedOption_KeepsRaw()
    {
        var model = new ModelOptions()
            .Name("")
            .OutputTokenLimit(0)
            .Raw("model", "raw-model")
            .Raw("output_token_limit", 64);

        AssertConfig("{\"model\":\"raw-model\",\"output_token_limit\":64}", ModelParameters.Create(model, null));
    }

    [Fact]
    public void Raw_SameKeyTwice_KeepsLastValue()
    {
        var model = new ModelOptions().Raw("temperature", 0.1).Raw("temperature", 0.9);

        AssertConfig("{\"temperature\":0.9}", ModelParameters.Create(model, null));
    }

    [Fact]
    public void Raw_NullValue_IsJsonNull()
    {
        AssertConfig("{\"seed\":null}", ModelParameters.Create(new ModelOptions().Raw("seed", null), null));
    }

    [Theory]
    [InlineData("cred_id")]
    [InlineData("Cred_Id")]
    [InlineData("api_key")]
    [InlineData("API_KEY")]
    [InlineData("")]
    [InlineData(null)]
    public void Raw_InvalidKey_Throws(string? key)
    {
        Assert.Throws<InvalidArgumentException>(() => new ModelOptions().Raw(key!, "value"));
    }

    [Fact]
    public void OutputTokenLimit_Negative_Throws()
    {
        Assert.Throws<InvalidArgumentException>(() => new ModelOptions().OutputTokenLimit(-1));
    }

    [Fact]
    public void Snapshot_IgnoresLaterChanges()
    {
        var headers = new JsonObject { ["x-team"] = "query" };
        var model = new ModelOptions().Provider("openai").Raw("headers", headers);

        var parameters = ModelParameters.Create(model, ModelCredential.ApiKey(Key));

        model.Provider("other").Name("other").Raw("extra", 1).Raw("headers", "replaced");
        headers["x-team"] = "changed";
        headers["x-new"] = "added";

        AssertConfig("{\"provider\":\"openai\",\"headers\":{\"x-team\":\"query\"},\"api_key\":\"" + Key + "\"}",
            parameters);
    }

    [Fact]
    public void Raw_NodeWithParent_IsCopied()
    {
        var parent = new JsonObject { ["temperature"] = 0.5 };
        var model = new ModelOptions().Raw("temperature", parent["temperature"]);

        AssertConfig("{\"temperature\":0.5}", ModelParameters.Create(model, null));
        Assert.Equal(0.5, parent["temperature"]!.GetValue<double>());
    }

    [Theory]
    [InlineData(null, "pw", "org")]
    [InlineData("", "pw", "org")]
    [InlineData("user@example.com", null, "org")]
    [InlineData("user@example.com", "", "org")]
    [InlineData("user@example.com", "pw", null)]
    [InlineData("user@example.com", "pw", "")]
    public void Capella_NullOrEmptyArgument_Throws(string? email, string? password, string? organizationId)
    {
        Assert.Throws<InvalidArgumentException>(() => ModelCredential.Capella(email!, password!, organizationId!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CredentialStoreAndApiKey_NullOrEmptyArgument_Throws(string? value)
    {
        Assert.Throws<InvalidArgumentException>(() => ModelCredential.CredentialStore(value!));
        Assert.Throws<InvalidArgumentException>(() => ModelCredential.ApiKey(value!));
    }

    [Fact]
    public void ToString_NeverContainsSecrets()
    {
        var capella = ModelCredential.Capella("user@example.com", Password, "org-1");
        var apiKey = ModelCredential.ApiKey(Key);
        var model = new ModelOptions().Provider("openai").Name("gpt-4o").Raw("headers", RawSecret);

        var texts = new[]
        {
            capella.ToString(),
            apiKey.ToString(),
            ModelCredential.CredentialStore("my-cred").ToString(),
            model.ToString(),
            ModelParameters.Create(new ModelOptions().Name("gpt-4o"), capella).ToString(),
            ModelParameters.Create(model, apiKey).ToString()
        };

        foreach (var text in texts)
        {
            Assert.DoesNotContain(Password, text);
            Assert.DoesNotContain(Key, text);
            Assert.DoesNotContain(RawSecret, text);
        }

        Assert.Equal("ModelCredential { Kind = Capella }", capella.ToString());
        Assert.Equal("ModelOptions { Provider = openai, Name = gpt-4o }", model.ToString());
    }

    private static void AssertConfig(string expectedJson, ModelParameters parameters)
    {
        Assert.NotNull(parameters.Config);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedJson), parameters.Config),
            parameters.Config!.ToJsonString());
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
