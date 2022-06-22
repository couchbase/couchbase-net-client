using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Eventing;
using Couchbase.UnitTests.Core.Diagnostics.Tracing;
using Couchbase.UnitTests.Core.Utils;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Management.Eventing
{
    public class EventingFunctionManagerTests : IDisposable
    {
        private readonly LoggerFactory _loggerFactory;

        public EventingFunctionManagerTests(ITestOutputHelper testOutputHelper)
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(new XUnitLoggerProvider(testOutputHelper));
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

        [Fact]
        public void When_NotConnected_EventingFunctionManager_Throws_NodeUnavailableException()
        {
            var clusterContext = new ClusterContext();
            var serviceUriProviderMock = new Mock<ServiceUriProvider>(clusterContext);

            var serviceUriProvider = serviceUriProviderMock.Object;
            Assert.Throws<ServiceNotAvailableException>(() => serviceUriProvider.GetRandomEventingUri());
        }

        [Fact]
        public async Task Test_GetAllFunctionsAsync_Ok()
        {
            using var response =
                ResourceHelper.ReadResourceAsStream(@"Documents\Eventing\getallfunctions-response.json");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            var eventingFunctions = await manager.GetAllFunctionsAsync();
            var eventingFunction = eventingFunctions.First();
            Assert.Equal("function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}", eventingFunction.Code);
            Assert.Equal("UhEbm2", eventingFunction.FunctionInstanceId);
            Assert.Equal("evt-7.0.0-5071-ee", eventingFunction.Version);
            Assert.False(eventingFunction.EnforceSchema);
            Assert.Equal(2908133798, eventingFunction.HandlerUuid);
        }

        [Fact]
        public async Task Test_GetFunctionAsync_Ok()
        {
            using var response =
                ResourceHelper.ReadResourceAsStream(@"Documents\Eventing\getfunction-response.json");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            var eventingFunctions = await manager.GetFunctionAsync("case_1_enrich_ips");
            var eventingFunction = eventingFunctions;
            Assert.Equal("function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}", eventingFunction.Code);
            Assert.Equal("UhEbm2", eventingFunction.FunctionInstanceId);
            Assert.Equal("evt-7.0.0-5071-ee", eventingFunction.Version);
            Assert.False(eventingFunction.EnforceSchema);
            Assert.Equal(2908133798, eventingFunction.HandlerUuid);
        }

        [Fact]
        public void Test_ToJson()
        {
            var originalJson = ResourceHelper.ReadResource(@"Documents\Eventing\getfunction-response.json");
            var eventingResource = JsonConvert.DeserializeObject<EventingFunction>(originalJson);

            var json = eventingResource.ToJson();
        }

        [Theory]
        [InlineData("200_ok_upsert.json", HttpStatusCode.OK, null)]
        [InlineData("400_err_invalid_config_upsert.json", HttpStatusCode.BadRequest, typeof(InvalidArgumentException))]
        [InlineData("422_err_handler_compilation.json", HttpStatusCode.UnprocessableEntity, typeof(EventingFunctionCompilationFailureException))]
        [InlineData("422_err_source_mb_same.json", HttpStatusCode.UnprocessableEntity, typeof(EventingFunctionIdenticalKeyspaceException))]
        [InlineData("500_err_collection_missing.json", HttpStatusCode.InternalServerError, typeof(Couchbase.Management.Collections.CollectionNotFoundException))]
        [InlineData("500_err_bucket_missing.json", HttpStatusCode.InternalServerError, typeof(BucketNotFoundException))]
        public async Task Test_UpsertAsync(string jsonFileName, HttpStatusCode statusCode, Type exception)
        {
            using var response =
                ResourceHelper.ReadResourceAsStream($@"Documents\Eventing\{jsonFileName}");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>(), It.IsAny<EventingFunction>()))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            try
            {
                await manager.UpsertFunctionAsync(new EventingFunction{Name = "some_func"});
            }
            catch (Exception e)
            {
                Assert.True(e.GetType() == exception, $"Expected {e.GetType().Name} but was {exception.Name}");
            }
        }

        [Theory]
        [InlineData("200_ok_upsert.json", HttpStatusCode.OK, null)]
        [InlineData("406_err_app_not_deployed.json", HttpStatusCode.NotAcceptable, typeof(EventingFunctionNotDeployedException))]
        [InlineData("422_err_app_not_undeployed.json", HttpStatusCode.UnprocessableEntity, typeof(EventingFunctionDeployedException))]
        [InlineData("404_err_app_not_found_ts.json", HttpStatusCode.NotFound, typeof(EventingFunctionNotFoundException))]
        public async Task Test_DropFunctionAsync(string jsonFileName, HttpStatusCode statusCode, Type exception)
        {
            using var response =
                ResourceHelper.ReadResourceAsStream($@"Documents\Eventing\{jsonFileName}");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            try
            {
                await manager.DropFunctionAsync("case_1_enrich_ips");
            }
            catch (Exception e)
            {
                Assert.True(e.GetType() == exception, $"Expected {e.GetType().Name} but was {exception.Name}");
            }
        }

        [Theory]
        [InlineData("200_ok_upsert.json", HttpStatusCode.OK, null)]
        [InlineData("423_err_app_not_bootstrapped.json", HttpStatusCode.Locked, typeof(EventingFunctionNotBootstrappedException))]
        [InlineData("404_err_app_not_found_ts.json", HttpStatusCode.NotFound, typeof(EventingFunctionNotFoundException))]
        public async Task Test_PauseFunctionAsync(string jsonFileName, HttpStatusCode statusCode, Type exception)
        {
            using var response =
                ResourceHelper.ReadResourceAsStream($@"Documents\Eventing\{jsonFileName}");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>(), null))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            try
            {
                await manager.PauseFunctionAsync("case_1_enrich_ips");
            }
            catch (Exception e)
            {
                Assert.True(e.GetType() == exception, $"Expected {exception.Name} but was {e.GetType().Name}");
            }
        }

        [Theory]
        [InlineData("200_ok_upsert.json", HttpStatusCode.OK, null)]
        [InlineData("406_err_app_not_deployed.json", HttpStatusCode.Locked, typeof(EventingFunctionNotDeployedException))]
        [InlineData("404_err_app_not_found_ts.json", HttpStatusCode.NotAcceptable, typeof(EventingFunctionNotFoundException))]
        public async Task Test_ResumeFunctionAsync(string jsonFileName, HttpStatusCode statusCode, Type exception)
        {
            using var response =
                ResourceHelper.ReadResourceAsStream($@"Documents\Eventing\{jsonFileName}");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>(), null))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            try
            {
                await manager.ResumeFunctionAsync("case_1_enrich_ips");
            }
            catch (Exception e)
            {
                Assert.True(e.GetType() == exception, $"Expected {e.GetType().Name} but was {exception.Name}");
            }
        }

        [Theory]
        [InlineData("200_ok_upsert.json", HttpStatusCode.OK, null)]
        [InlineData("423_err_app_not_bootstrapped.json", HttpStatusCode.Locked, typeof(EventingFunctionNotBootstrappedException))]
        [InlineData("404_err_app_not_found_ts.json", HttpStatusCode.NotAcceptable, typeof(EventingFunctionNotFoundException))]
        public async Task Test_DeployFunctionAsync(string jsonFileName, HttpStatusCode statusCode, Type exception)
        {
            using var response =
                ResourceHelper.ReadResourceAsStream($@"Documents\Eventing\{jsonFileName}");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>(), null))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            try
            {
                await manager.DeployFunctionAsync("case_1_enrich_ips");
            }
            catch (Exception e)
            {
                Assert.True(e.GetType() == exception, $"Expected {e.GetType().Name} but was {exception.Name}");
            }
        }

        [Theory]
        [InlineData("200_ok_upsert.json", HttpStatusCode.OK, null)]
        [InlineData("404_page_not_found.json", HttpStatusCode.NotFound, typeof(CouchbaseException))]
        [InlineData("406_err_app_not_deployed.json", HttpStatusCode.Locked, typeof(EventingFunctionNotDeployedException))]
        [InlineData("404_err_app_not_found_ts.json", HttpStatusCode.NotAcceptable, typeof(EventingFunctionNotFoundException))]
        public async Task Test_UndeployFunctionAsync(string jsonFileName, HttpStatusCode statusCode, Type exception)
        {
            using var response =
                ResourceHelper.ReadResourceAsStream($@"Documents\Eventing\{jsonFileName}");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>(), null))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            try
            {
                await manager.UndeployFunctionAsync("case_1_enrich_ips");
            }
            catch (Exception e)
            {
                Assert.True(e.GetType() == exception, $"Expected {e.GetType().Name} but was {exception.Name}");
            }
        }

        [Fact]
        public async Task Test_FunctionStatus()
        {
            using var response =
                ResourceHelper.ReadResourceAsStream(@"Documents\Eventing\200_ok_status.json");
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(buffer)
            };

            var serviceMock = new Mock<IEventingFunctionService>();
            serviceMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(httpResponseMessage));

            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            var manager = new EventingFunctionManager(serviceMock.Object,
                new Mock<ILogger<EventingFunctionManager>>().Object, tracer);

            var functionStatus = await manager.FunctionsStatus();
            Assert.Equal(1, functionStatus.NumEventingNodes);

            var function = functionStatus.Functions.First();
            Assert.Equal(EventingFunctionStatus.Undeployed, function.Status);
            Assert.Equal(EventingFunctionDeploymentStatus.Undeployed, function.DeploymentStatus);
            Assert.Equal(EventingFunctionProcessingStatus.Paused, function.ProcessingStatus);
            Assert.Equal(0, function.NumDeployedNodes);
            Assert.Equal(0, function.NumBootstrappingNodes);
            Assert.Equal("case_1_enrich_ips", function.Name);
        }
    }
}
