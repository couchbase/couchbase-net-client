using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Tests.Fakes;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class OperationExecutorTests
    {
        private CouchbaseRequestExecuter _requestExecuter;
        private string _bucketName = "default";
        private readonly ConcurrentDictionary<uint, IOperation> _pending = new ConcurrentDictionary<uint, IOperation>();

        readonly IPEndPoint _endPoint = UriExtensions.GetEndPoint(ConfigurationManager.AppSettings["OperationTestAddress"]);
        private readonly FakeConnectionPool _connectionPool = new FakeConnectionPool();
        readonly IBucketConfig _bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(ResourceHelper.ReadResource("Data\\Configuration\\config-revision-8934.json"));
        readonly IByteConverter _converter = new DefaultConverter();
        readonly ITypeTranscoder _transcoder = new DefaultTranscoder(new ManualByteConverter(), new DefaultSerializer());

        internal IClusterController GetBucketForKey(string key, out IConfigInfo configInfo)
        {
            var config = new ClientConfiguration();
            var fakeServer = new FakeServer(_connectionPool, null, null, _endPoint,
                new FakeIOService(_endPoint, _connectionPool, false));

            var mockVBucket = new Mock<IVBucket>();
            mockVBucket.Setup(x => x.LocatePrimary()).Returns(fakeServer);

            var mockKeyMapper = new Mock<IKeyMapper>();
            mockKeyMapper.Setup(x => x.MapKey(key, It.IsAny<uint>())).Returns(mockVBucket.Object);

            var mockConfigInfo = new Mock<IConfigInfo>();
            mockConfigInfo.Setup(x => x.GetKeyMapper()).Returns(mockKeyMapper.Object);
            mockConfigInfo.Setup(x => x.BucketConfig).Returns(_bucketConfig);
            mockConfigInfo.Setup(x => x.GetServer()).Returns(fakeServer);
            mockConfigInfo.Setup(x => x.IsDataCapable).Returns(true);
            mockConfigInfo.Setup(x => x.IsViewCapable).Returns(true);
            mockConfigInfo.Setup(x => x.IsQueryCapable).Returns(true);
            mockConfigInfo.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());
            configInfo = mockConfigInfo.Object;

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(config);
            mockController.Setup(x => x.CreateBucket("default", ""))
                .Returns(new CouchbaseBucket(mockController.Object, "default", _converter, _transcoder));

            var cluster = new Cluster(config, mockController.Object);
            var bucket = cluster.OpenBucket("default", "");

            //simulate a config event
            ((IConfigObserver)bucket).NotifyConfigChanged(mockConfigInfo.Object);

            return mockController.Object;
        }

        [Test]
        public async Task Test_Executer()
        {
            IConfigInfo configInfo;
            var controller = GetBucketForKey("thekey", out configInfo);
            _requestExecuter = new CouchbaseRequestExecuter(controller, configInfo, "default", _pending);

            var tcs = new TaskCompletionSource<IOperationResult<string>>();
            var cts = new CancellationTokenSource();

            var operation = new Mock<IOperation<string>>();
            operation.Setup(x => x.GetConfig()).Returns(new BucketConfig());
            operation.Setup(x => x.Write()).Throws(new Exception("bad kungfu"));
            operation.Setup(x => x.Key).Returns("thekey");
            operation.Setup(x => x.Completed).Returns(CallbackFactory.CompletedFuncWithRetryForCouchbase(
                _requestExecuter, _pending, controller, tcs, cts.Token));
            operation.Setup(x => x.GetResultWithValue()).Returns(new OperationResult<string>{Success = true});

            var result = await _requestExecuter.SendWithRetryAsync(operation.Object, tcs);
            Assert.IsTrue(result.Success);
        }

        [SetUp]
        public void SetUp()
        {
            _connectionPool.Clear();

            var server = new Mock<IServer>();
            server.Setup(x => x.Send(It.IsAny<IOperation<Object>>())).Returns(new OperationResult<object>());
            var vBucket = new Mock<IVBucket>();
            vBucket.Setup(x => x.LocatePrimary()).Returns(server.Object);

            var keyMapper = new Mock<IKeyMapper>();
            keyMapper.Setup(x => x.MapKey(It.IsAny<string>(), It.IsAny<uint>())).Returns(vBucket.Object);

            var configInfo = new Mock<IConfigInfo>();
            configInfo.Setup(x => x.GetKeyMapper()).Returns(keyMapper.Object);
            configInfo.Setup(x => x.IsDataCapable).Returns(true);
            configInfo.Setup(x => x.IsViewCapable).Returns(true);
            configInfo.Setup(x => x.IsQueryCapable).Returns(true);
            configInfo.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Configuration).Returns(new ClientConfiguration());

            _requestExecuter = new CouchbaseRequestExecuter(clusterController.Object, configInfo.Object,  _bucketName, _pending);
        }

        [Test]
        public async Task When_Operation_WriteAsync_Faults_Success_Is_False()
        {
            IConfigInfo configInfo;
            var controller = GetBucketForKey("thekey", out configInfo);
            _requestExecuter = new CouchbaseRequestExecuter(controller, configInfo, "default", _pending);

            var tcs = new TaskCompletionSource<IOperationResult<string>>();
            var cts = new CancellationTokenSource();

            var operation = new Mock<IOperation<string>>();
            operation.Setup(x => x.GetConfig()).Returns(new BucketConfig());
            operation.Setup(x => x.WriteAsync()).Throws(new Exception("bad kungfu"));
            operation.Setup(x => x.Key).Returns("thekey");
            operation.Setup(x => x.Completed).Returns(CallbackFactory.CompletedFuncWithRetryForCouchbase(
                _requestExecuter, _pending, controller, tcs, cts.Token));
            operation.Setup(x => x.GetResultWithValue()).Returns(new OperationResult<string>
            {
                Success = false, Exception = new Exception("bad kungfu")
            });

            var result = await _requestExecuter.SendWithRetryAsync(operation.Object, tcs);
            Assert.AreEqual(false, result.Success);
        }

        [Test]
        public void When_Operation_Is_Set_Operation_And_ResponseStatus_Is_NMV_Allow_Retries()
        {
            var operation = new Mock<IOperation<string>>();
            operation.Setup(x => x.GetConfig()).Returns(new BucketConfig());
            var operationResult = new OperationResult<string>
            {
                Status = ResponseStatus.VBucketBelongsToAnotherServer
            };

            var result = _requestExecuter.CanRetryOperation(operationResult, operation.Object);
            Assert.AreEqual(true, result);
        }


        [Test]
        public void When_Operation_Is_Successful_It_Does_Not_Timeout()
        {
            var slowSet = new SlowSet<object>(
                "When_Operation_Is_Slow_Operation_TimesOut_Key",
                "When_Operation_Is_Slow_Operation_TimesOut",
                new DefaultTranscoder(),
                null,
                500)
            {
                SleepTime = 1000
            };

            var result = _requestExecuter.SendWithRetry(slowSet);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void When_Operation_Is_Faster_Than_Timeout_Operation_Succeeds()
        {
            var slowSet = new SlowSet<object>(
                "When_Operation_Is_Slow_Operation_TimesOut_Key",
                "When_Operation_Is_Slow_Operation_TimesOut",
                new DefaultTranscoder(),
                null,
                1000)
            {
                SleepTime = 500
            };

            var result = _requestExecuter.SendWithRetry(slowSet);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void When_Timeout_Defaults_Are_Used_Operation_Succeeds()
        {
            var slowSet = new SlowSet<object>(
                "When_Operation_Is_Slow_Operation_TimesOut_Key",
                "When_Operation_Is_Slow_Operation_TimesOut",
                new DefaultTranscoder(),
                null,
                new ClientConfiguration().DefaultOperationLifespan); //use lifespan in configuration

            var result = _requestExecuter.SendWithRetry(slowSet);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }
    }
}
