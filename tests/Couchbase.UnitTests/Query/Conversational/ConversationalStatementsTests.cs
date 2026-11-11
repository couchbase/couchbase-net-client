using System;
using Couchbase.Core.Exceptions;
using Couchbase.Query.Conversational;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational;

public class ConversationalStatementsTests
{
    private static readonly (string, string, string)[] Hotel = { ("travel-sample", "inventory", "hotel") };

    private const string HotelJson = "\"keyspaces\":[\"`travel-sample`.`inventory`.`hotel`\"]";

    [Fact]
    public void UsingAi_KeyspacesOnly_OmitsUnsetFields()
    {
        Assert.Equal("USING AI WITH {" + HotelJson + "} list hotels in Paris",
            ConversationalStatements.UsingAi("list hotels in Paris", Hotel));
    }

    [Fact]
    public void UsingAi_AllFields_KeepsFieldOrder()
    {
        var statement = ConversationalStatements.UsingAi("list hotels", Hotel, knowledge: true,
            hint: "use the city field", execute: false, output: ConversationalQueryOutput.FtsSqlpp);

        Assert.Equal(
            "USING AI WITH {" + HotelJson +
            ",\"knowledge\":true,\"hint\":\"use the city field\",\"execute\":false,\"output\":\"FTSSQL\"} list hotels",
            statement);
    }

    [Fact]
    public void UsingAi_KnowledgeFalse_IsSent()
    {
        Assert.Equal("USING AI WITH {" + HotelJson + ",\"knowledge\":false} list hotels",
            ConversationalStatements.UsingAi("list hotels", Hotel, knowledge: false));
    }

    [Fact]
    public void UsingAi_EscapesKeyspacePaths()
    {
        var keyspaces = new[]
        {
            ("travel-sample", "inventory", "hotel"),
            ("my`bucket", "_default", "`_default`")
        };

        Assert.Equal(
            "USING AI WITH {\"keyspaces\":[\"`travel-sample`.`inventory`.`hotel`\",\"`my``bucket`.`_default`.`_default`\"]} count docs",
            ConversationalStatements.UsingAi("count docs", keyspaces));
    }

    [Fact]
    public void UsingAi_PromptIsPlainText()
    {
        Assert.Equal("USING AI WITH {" + HotelJson + "} find \"luxury\" hotels\nin Nice",
            ConversationalStatements.UsingAi("find \"luxury\" hotels\nin Nice", Hotel));
    }

    [Fact]
    public void UsingAi_HintIsJsonEncoded()
    {
        Assert.Equal("USING AI WITH {" + HotelJson + ",\"hint\":\"say \\\"hi\\\"\"} list hotels",
            ConversationalStatements.UsingAi("list hotels", Hotel, hint: "say \"hi\""));
    }

    [Theory]
    [InlineData(ConversationalQueryOutput.Sqlpp, "SQL")]
    [InlineData(ConversationalQueryOutput.FtsSqlpp, "FTSSQL")]
    [InlineData(ConversationalQueryOutput.JsUdf, "JSUDF")]
    public void UsingAi_MapsOutput(ConversationalQueryOutput output, string expected)
    {
        Assert.Equal("USING AI WITH {" + HotelJson + ",\"output\":\"" + expected + "\"} list hotels",
            ConversationalStatements.UsingAi("list hotels", Hotel, output: output));
    }

    [Fact]
    public void UsingAi_UnknownOutput_Throws()
    {
        Assert.Throws<InvalidArgumentException>(() =>
            ConversationalStatements.UsingAi("list hotels", Hotel, output: (ConversationalQueryOutput)42));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Prompt_NullOrEmpty_Throws(string? prompt)
    {
        Assert.Throws<InvalidArgumentException>(() => ConversationalStatements.UsingAi(prompt!, Hotel));
        Assert.Throws<InvalidArgumentException>(() => ConversationalStatements.ChatTurn(prompt!));
    }

    [Fact]
    public void Keyspaces_Empty_Throws()
    {
        var none = Array.Empty<(string, string, string)>();
        Assert.Throws<InvalidArgumentException>(() => ConversationalStatements.UsingAi("list hotels", none));
        Assert.Throws<InvalidArgumentException>(() => ConversationalStatements.BeginChat(none));
    }

    [Theory]
    [InlineData("", "inventory", "hotel")]
    [InlineData("travel-sample", null, "hotel")]
    [InlineData("travel-sample", "inventory", "")]
    public void Keyspaces_EmptyName_Throws(string bucket, string? scope, string collection)
    {
        var keyspaces = new[] { (bucket, scope!, collection) };
        Assert.Throws<InvalidArgumentException>(() => ConversationalStatements.UsingAi("list hotels", keyspaces));
        Assert.Throws<InvalidArgumentException>(() => ConversationalStatements.BeginChat(keyspaces));
    }

    [Fact]
    public void ChatTurn_NoOptions_SendsEmptyWith()
    {
        Assert.Equal("USING AI WITH {} and in Lyon?", ConversationalStatements.ChatTurn("and in Lyon?"));
    }

    [Fact]
    public void ChatTurn_EmptyHint_IsOmitted()
    {
        Assert.Equal("USING AI WITH {} and in Lyon?", ConversationalStatements.ChatTurn("and in Lyon?", hint: ""));
    }

    [Fact]
    public void ChatTurn_AllFields_KeepsFieldOrder()
    {
        Assert.Equal(
            "USING AI WITH {\"hint\":\"cheap ones\",\"execute\":true,\"output\":\"JSUDF\"} and in Lyon?",
            ConversationalStatements.ChatTurn("and in Lyon?", hint: "cheap ones", execute: true,
                output: ConversationalQueryOutput.JsUdf));
    }

    [Fact]
    public void BeginChat_KnowledgeAndTimeout_KeepsFieldOrder()
    {
        Assert.Equal("BEGIN CHAT WITH {" + HotelJson + ",\"knowledge\":true,\"timeout\":1800}",
            ConversationalStatements.BeginChat(Hotel, knowledge: true, timeout: TimeSpan.FromMinutes(30)));
    }

    [Theory]
    [InlineData(1L, 1L)]
    [InlineData(12_000_000L, 2L)]
    [InlineData(50_000_000L, 5L)]
    [InlineData(50_000_001L, 6L)]
    public void BeginChat_Timeout_RoundsUpToSeconds(long ticks, long expectedSeconds)
    {
        Assert.Equal("BEGIN CHAT WITH {" + HotelJson + ",\"knowledge\":false,\"timeout\":" + expectedSeconds + "}",
            ConversationalStatements.BeginChat(Hotel, timeout: TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void BeginChat_ZeroTimeout_IsOmitted()
    {
        Assert.Equal("BEGIN CHAT WITH {" + HotelJson + ",\"knowledge\":false}",
            ConversationalStatements.BeginChat(Hotel, timeout: TimeSpan.Zero));
    }

    [Fact]
    public void BeginChat_NegativeTimeout_Throws()
    {
        Assert.Throws<InvalidArgumentException>(() =>
            ConversationalStatements.BeginChat(Hotel, timeout: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void PauseChat_NoSummarize_HasNoWith()
    {
        Assert.Equal("PAUSE CHAT", ConversationalStatements.PauseChat());
    }

    [Theory]
    [InlineData(true, "PAUSE CHAT WITH {\"summarize\":true}")]
    [InlineData(false, "PAUSE CHAT WITH {\"summarize\":false}")]
    public void PauseChat_Summarize_IsSent(bool summarize, string expected)
    {
        Assert.Equal(expected, ConversationalStatements.PauseChat(summarize));
    }

    [Fact]
    public void ResumeAndEnd_HaveNoWith()
    {
        Assert.Equal("RESUME CHAT", ConversationalStatements.ResumeChat);
        Assert.Equal("END CHAT", ConversationalStatements.EndChat);
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
