using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Operations;
using System.Collections.Generic;
using System.Text.Json;
using Couchbase.Core.Exceptions;
using Couchbase.Query;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Query;

public class QueryResultExtensionsTests
{
    [Fact]
    public void Test_QueryContext_UnknownParameter()
    {
        string errorContextJson = @"{
    ""statement"": ""REDACTED"",
    ""clientContextId"": ""4f8ad847-e14d-4892-b4ee-1a32a47e20dc"",
    ""parameters"": ""{\\u0022Named\\u0022:{},\\u0022Raw\\u0022:{},\\u0022Positional\\u0022:[]}"",
    ""httpStatus"": ""BadRequest"",
    ""queryStatus"": ""fatal"",
    ""errors"": [
        {
            ""msg"": ""Unrecognized parameter in request: query_context"",
            ""code"": 1065,
            ""name"": null,
            ""severity"": 0,
            ""temp"": false,
            ""reason"": null,
            ""retry"": false
        }
    ],
    ""message"": ""Unrecognized parameter in request: query_context [1065]"",
    ""retryReasons"": null
}";

        List<Error> errors = new()
        {
            new Error()
            {
                Code = 1065,
                Message = "Unrecognized parameter in request: query_context [1065]",
                Severity = 0,
                Retry = false
            }
        };

        QueryErrorContext errorContext = JsonSerializer.Deserialize<QueryErrorContext>(errorContextJson);
        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, errorContext);
        Assert.IsAssignableFrom<FeatureNotAvailableException>(ex);
    }

    [Fact]
    public void Test_Index_Does_Not_Exist()
    {
        List<Error> errors = new()
        {
            new Error
            {
                Code = 5000,
                Message = "GSI Drop() - cause: Index does not exist.",
                Severity = 0,
                Retry = false
            }
        };

        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, new QueryErrorContext());
        Assert.IsAssignableFrom<IndexNotFoundException>(ex);
    }
}
