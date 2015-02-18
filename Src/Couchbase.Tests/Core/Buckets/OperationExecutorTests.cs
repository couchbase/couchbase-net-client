using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Tests.Fakes;
using Moq;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class OperationExecutorTests
    {
        private CouchbaseRequestExecuter _requestExecuter;
        private string _bucketName = "default";
        private readonly ConcurrentDictionary<uint, IOperation> _pending = new ConcurrentDictionary<uint, IOperation>();

        [SetUp]
        public void SetUp()
        {
            var server = new Mock<IServer>();
            server.Setup(x => x.Send(It.IsAny<IOperation<Object>>())).Returns(new OperationResult<object>());
            var vBucket = new Mock<IVBucket>();
            vBucket.Setup(x => x.LocatePrimary()).Returns(server.Object);

            var keyMapper = new Mock<IKeyMapper>();
            keyMapper.Setup(x => x.MapKey(It.IsAny<string>())).Returns(vBucket.Object);

            var configInfo = new Mock<IConfigInfo>();
            configInfo.Setup(x => x.GetKeyMapper()).Returns(keyMapper.Object);

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Configuration).Returns(new ClientConfiguration());

            _requestExecuter = new CouchbaseRequestExecuter(clusterController.Object, configInfo.Object,  _bucketName, _pending);
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
                new DefaultTranscoder(new AutoByteConverter()),
                null,
                new AutoByteConverter(),
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
                new DefaultTranscoder(new AutoByteConverter()),
                null,
                new AutoByteConverter(),
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
                new DefaultTranscoder(new AutoByteConverter()),
                null,
                new AutoByteConverter(),
                new ClientConfiguration().DefaultOperationLifespan); //use lifespan in configuration

            var result = _requestExecuter.SendWithRetry(slowSet);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }
    }
}
