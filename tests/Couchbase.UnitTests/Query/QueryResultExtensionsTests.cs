using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Operations;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Couchbase.Core.Exceptions;
using Couchbase.Core.RateLimiting;
using Couchbase.Query;
using Couchbase.Query.Conversational;
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

    [Theory]
    // 2120 is the *only* admin code that maps to AuthenticationFailure; RFC 58 revision 21 added the
    // single code, not the 2xxx range. These are its real neighbours in the admin family, plus the
    // codes either side of it, so widening the check to a range would fail here.
    [InlineData(2110, "Error creating metric")]
    [InlineData(2119, "Unassigned admin code")]
    [InlineData(2121, "Unassigned admin code")]
    [InlineData(2130, "The admin endpoint encountered an error.")]
    public void Test_Adjacent_Admin_Codes_Are_Not_AuthenticationFailure(int code, string message)
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

        // Exact type, not IsAssignableFrom: every mapped exception derives from CouchbaseException, so
        // IsAssignableFrom would still pass if these codes were mis-mapped to AuthenticationFailure.
        Assert.IsType<CouchbaseException>(ex);
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

    [Theory]
    // "msg" and "reason" are both optional in the server's error payload - the fixture in
    // Test_QueryContext_UnknownParameter shows a real response carrying "reason": null. Mapping must fall
    // through to the code-only result rather than throwing an NRE while building the exception, which would
    // replace the server's error with a meaningless one.
    [InlineData(null, null)]
    [InlineData("Some DML failure the SDK has no special mapping for", null)]
    [InlineData(null, 99999)]
    public void Test_Missing_Message_And_Reason_Do_Not_Throw(string message, int? reasonCode)
    {
        List<Error> errors = new()
        {
            new Error
            {
                Code = 12009,
                Message = message,
                Reason = reasonCode.HasValue ? new Reason { Code = reasonCode.Value } : null,
                Severity = 0,
                Retry = false
            }
        };

        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, new QueryErrorContext());
        Assert.IsAssignableFrom<DmlFailureException>(ex);
    }

    [Fact]
    public void Test_Reason_Code_Still_Maps_When_Message_Is_Null()
    {
        List<Error> errors = new()
        {
            new Error
            {
                Code = 12009,
                Message = null,
                Reason = new Reason { Code = 17014 },
                Severity = 0,
                Retry = false
            }
        };

        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, new QueryErrorContext());
        Assert.IsAssignableFrom<DocumentNotFoundException>(ex);
    }

    [Theory]
    // RFC 58: an HTTP 400 is an invalid argument unless the error code has its own mapping.
    [InlineData(1050, "No statement or prepared value", HttpStatusCode.BadRequest, typeof(InvalidArgumentException))]
    [InlineData(1065, "Unrecognized parameter in request: preserve_expiry", HttpStatusCode.BadRequest, typeof(InvalidArgumentException))]
    [InlineData(3000, "syntax error - line 1, column 1", HttpStatusCode.BadRequest, typeof(ParsingFailureException))]
    [InlineData(4100, "No index available on keyspace", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
    [InlineData(1050, "No statement or prepared value", HttpStatusCode.ServiceUnavailable, typeof(CouchbaseException))]
    [InlineData(1050, "No statement or prepared value", HttpStatusCode.OK, typeof(CouchbaseException))]
    public void Test_Http_400_Falls_Back_To_InvalidArgument(int code, string message, HttpStatusCode httpStatus, Type expectedType)
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

        QueryErrorContext errorContext = new()
        {
            Errors = errors,
            HttpStatus = httpStatus,
            QueryStatus = QueryStatus.Fatal
        };

        var mockQueryResult = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        mockQueryResult.Setup(qr => qr.Errors).Returns(errors);
        var ex = QueryResultExtensions.CreateExceptionForError(mockQueryResult.Object, errorContext);

        // Exact type: every mapped exception derives from CouchbaseException.
        Assert.IsType(expectedType, ex);
        Assert.Same(errorContext, ex.Context);
    }

    #region Conversational

    [Theory]
    [InlineData(19220, nameof(ConversationalOperation.Ask), typeof(RateLimitedException))]
    [InlineData(19221, nameof(ConversationalOperation.Query), typeof(RateLimitedException))]
    [InlineData(19222, nameof(ConversationalOperation.BeginChat), typeof(FeatureNotAvailableException))]
    [InlineData(19236, nameof(ConversationalOperation.Ask), typeof(ChatCurrentlyUnavailableException))]
    [InlineData(19236, nameof(ConversationalOperation.Pause), typeof(ChatCurrentlyUnavailableException))]
    [InlineData(19236, nameof(ConversationalOperation.End), typeof(ChatCurrentlyUnavailableException))]
    [InlineData(19236, nameof(ConversationalOperation.BeginChat), typeof(CouchbaseException))]
    [InlineData(19236, nameof(ConversationalOperation.Resume), typeof(CouchbaseException))]
    [InlineData(19236, nameof(ConversationalOperation.Query), typeof(CouchbaseException))]
    [InlineData(19240, nameof(ConversationalOperation.Ask), typeof(TemporaryFailureException))]
    [InlineData(19241, nameof(ConversationalOperation.Query), typeof(AuthenticationFailureException))]
    [InlineData(19243, nameof(ConversationalOperation.Ask), typeof(CouchbaseException))]
    [InlineData(19255, nameof(ConversationalOperation.Ask), typeof(CouchbaseException))]
    public void Conversational_Error_Maps_To_Exception(int code, string operation, Type expected)
    {
        var errors = new List<Error> { new() { Code = code, Message = "cq error", Reason = new Reason { Code = 1 } } };
        var context = ConversationalContext(errors, operation);

        var ex = CreateException(errors, context);

        Assert.IsType(expected, ex);
        Assert.Same(context, ex.Context);
        var error = Assert.Single(context.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal("cq error", error.Message);
        Assert.Equal(1, error.Reason.Code);
        if (ex is RateLimitedException rateLimited)
        {
            Assert.Equal(RateLimitedReason.ConcurrentRequestLimitReached, rateLimited.Reason);
        }
        if (ex is ChatCurrentlyUnavailableException chat)
        {
            Assert.Equal("chat-1", chat.ChatId);
            Assert.Equal("uuid-a", chat.OwnerNodeUuid);
            Assert.Equal(operation.ToLowerInvariant(), chat.Operation);
        }
    }

    [Fact]
    public void Conversational_Error_19243_With_Nested_19240_Is_TemporaryFailure()
    {
        var errors = new List<Error> { new() { Code = 19243, Reason = new Reason { Code = 19240, Message = "busy" } } };
        var context = ConversationalContext(errors, nameof(ConversationalOperation.Ask));

        var ex = CreateException(errors, context);

        Assert.IsType<TemporaryFailureException>(ex);
        Assert.Same(context, ex.Context);
    }

    [Theory]
    [InlineData(19240, 3000, typeof(TemporaryFailureException))]
    [InlineData(3000, 19240, typeof(ParsingFailureException))]
    [InlineData(19243, 3000, typeof(CouchbaseException))]
    [InlineData(19255, 19241, typeof(CouchbaseException))]
    [InlineData(99999, 19241, typeof(CouchbaseException))]
    public void Conversational_First_Error_Decides(int first, int second, Type expected)
    {
        var errors = new List<Error> { new() { Code = first }, new() { Code = second } };

        var ex = CreateException(errors, ConversationalContext(errors, nameof(ConversationalOperation.Ask)));

        Assert.IsType(expected, ex);
    }

    [Theory]
    [InlineData(19220, 0)]
    [InlineData(19221, 0)]
    [InlineData(19222, 0)]
    [InlineData(19236, 0)]
    [InlineData(19240, 0)]
    [InlineData(19241, 0)]
    [InlineData(19243, 19240)]
    [InlineData(19255, 0)]
    public void Ordinary_Query_Keeps_Todays_Mapping_For_Conversational_Codes(int code, int reasonCode)
    {
        var errors = new List<Error> { new() { Code = code, Reason = new Reason { Code = reasonCode } } };

        var ex = CreateException(errors, new QueryErrorContext { Errors = errors });

        Assert.IsType<CouchbaseException>(ex);
    }

    private static QueryErrorContext ConversationalContext(List<Error> errors, string operation)
    {
        var op = (ConversationalOperation)Enum.Parse(typeof(ConversationalOperation), operation);
        return new QueryErrorContext
        {
            Message = "cq error [code]",
            Errors = errors,
            Conversational = new ConversationalRequest(op, op == ConversationalOperation.Query ? null : "chat-1", "uuid-a")
        };
    }

    private static CouchbaseException CreateException(List<Error> errors, QueryErrorContext context)
    {
        var result = new Mock<IQueryResult<object>>(MockBehavior.Strict);
        result.Setup(qr => qr.Errors).Returns(errors);
        return QueryResultExtensions.CreateExceptionForError(result.Object, context);
    }

    #endregion
}
