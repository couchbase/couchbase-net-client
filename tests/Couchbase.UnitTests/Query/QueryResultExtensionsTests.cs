using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Operations;
using System.Collections.Generic;
using System.Net;
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

    [Theory]
    // Error range 10xxx, per RFC 58.
    [InlineData(10000, "User does not have credentials to run queries")]
    [InlineData(10999, "User does not have credentials to run queries")]
    [InlineData(13014, "Unable to authorize user")]
    // Code 2120, added in RFC 58 revision 21. The message must not be taken into account.
    [InlineData(2120, "Error authorizing against cluster cause: Failure to authenticate user")]
    [InlineData(2120, "Error authorizing against cluster")]
    [InlineData(2120, null)]
    public void Test_AuthenticationFailure_Codes(int code, string message)
    {
        List<Error> errors = new()
        {
            new Error
            {
                Code = code,
                Message = message,
                Severity = 0,
                Retry = false
            }
        };

        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, new QueryErrorContext());
        Assert.IsAssignableFrom<AuthenticationFailureException>(ex);
    }

    [Fact]
    public void Test_AuthenticationFailure_Preserves_Server_Explanation()
    {
        // A locked account is reported as code 2120; the server's explanation is the only way the user can
        // tell why authentication failed, so it must survive into the exception. See NCBC-3962.
        const string serverMessage = "Error authorizing against cluster cause: Failure to authenticate user";

        List<Error> errors = new()
        {
            new Error
            {
                Code = 2120,
                Message = serverMessage,
                Severity = 0,
                Retry = false
            }
        };

        // QueryClient composes the context message as "{msg} [{code}]" from the first error.
        QueryErrorContext errorContext = new()
        {
            Statement = "SELECT 1;",
            Message = $"{serverMessage} [2120]",
            Errors = errors,
            HttpStatus = HttpStatusCode.Unauthorized,
            QueryStatus = QueryStatus.Fatal
        };

        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, errorContext);

        Assert.IsAssignableFrom<AuthenticationFailureException>(ex);
        Assert.Contains(serverMessage, ex.Message);
        Assert.Same(errorContext, ex.Context);
        var error = Assert.Single(Assert.IsType<QueryErrorContext>(ex.Context).Errors);
        Assert.Equal(2120, error.Code);
        Assert.Equal(serverMessage, error.Message);
    }
}
