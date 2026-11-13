using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Query.Conversational;
using Moq;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational;

// Records each request and answers with canned JSON responses or exceptions, in order.
internal sealed class FakeConversationalQueryContext : IConversationalQueryContext
{
    public const string ChatId = "23a2c880-4566-4f78-9e81-86ea755b2a7f";

    public const string Success = """{"requestID":"d4c7cdd0-8f6b-4a0e-9b1d-3f2a6c5e7b90","status":"success"}""";

    public const string BeginChatSuccess =
        """{"requestID":"d4c7cdd0-8f6b-4a0e-9b1d-3f2a6c5e7b90","status":"success","chatId":"23a2c880-4566-4f78-9e81-86ea755b2a7f"}""";

    private readonly Queue<Func<string>> _responses = new();

    public List<Request> Requests { get; } = new();

    public List<IClusterNode> QueryNodes { get; } = new();

    public bool EnableTls { get; set; }

    public bool? HasConversationalQueryCapability { get; set; } = true;

    public Exception? BootstrapException { get; set; }

    public IEnumerable<IClusterNode> Nodes => QueryNodes;

    public static FakeConversationalQueryContext WithNodes(params string[] nodeUuids)
    {
        var context = new FakeConversationalQueryContext();
        foreach (var uuid in nodeUuids)
        {
            context.QueryNodes.Add(Node(uuid, $"http://{uuid}:8093/query/service"));
        }

        return context;
    }

    public FakeConversationalQueryContext Respond(string json)
    {
        _responses.Enqueue(() => json);
        return this;
    }

    public FakeConversationalQueryContext Throw(Exception exception)
    {
        _responses.Enqueue(() => throw exception);
        return this;
    }

    public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options)
    {
        var request = new Request(statement, options);
        Requests.Add(request);

        var json = _responses.Count > 0 ? _responses.Dequeue()() : Success;
        request.ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = new StreamingQueryResult<T>(request.ResponseStream, DefaultSerializer.Instance,
            (_, _) => new QueryErrorContext()) { Success = true };
        await result.InitializeAsync();
        return result;
    }

    public void ThrowIfNotBootstrapped()
    {
        if (BootstrapException is not null)
        {
            throw BootstrapException;
        }
    }

    public static IClusterNode Node(string nodeUuid, string queryUri)
    {
        var uri = new Uri(queryUri);
        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.NodeUuid).Returns(nodeUuid);
        node.SetupGet(x => x.HasQuery).Returns(true);
        node.SetupGet(x => x.QueryUri).Returns(uri);
        node.SetupGet(x => x.NodesAdapter)
            .Returns(new NodeAdapter { Hostname = uri.Host, N1Ql = uri.Port, N1QlSsl = uri.Port });
        return node.Object;
    }

    public static ICouchbaseCollection Collection(string bucket, string scope, string collection)
    {
        var bucketMock = new Mock<IBucket>();
        bucketMock.SetupGet(x => x.Name).Returns(bucket);
        var scopeMock = new Mock<IScope>();
        scopeMock.SetupGet(x => x.Name).Returns(scope);
        scopeMock.SetupGet(x => x.Bucket).Returns(bucketMock.Object);
        var collectionMock = new Mock<ICouchbaseCollection>();
        collectionMock.SetupGet(x => x.Name).Returns(collection);
        collectionMock.SetupGet(x => x.Scope).Returns(scopeMock.Object);
        return collectionMock.Object;
    }

    public static QueryErrorContext ErrorContext(int code) =>
        new() { Errors = new List<Error> { new() { Code = code, Message = "error " + code } } };

    internal sealed class Request
    {
        public Request(string statement, QueryOptions options)
        {
            Statement = statement;
            Options = options;
        }

        public string Statement { get; }

        public QueryOptions Options { get; }

        public ConversationalRequest Conversational => Options.Conversational!;

        public MemoryStream? ResponseStream { get; set; }

        // The JSON body Query would receive.
        public JsonElement Body
        {
            get
            {
                var copy = Options.Clone();
                copy.Statement(Statement);
                return JsonDocument.Parse(copy.GetFormValuesAsJson()).RootElement;
            }
        }

        public bool ResultDisposed => ResponseStream is { CanRead: false };
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
