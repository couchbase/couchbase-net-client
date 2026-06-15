using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.UnitTests.Helpers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Utils;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using System.Linq;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class ResponseStatusExtensionTests
    {
        private readonly ITestOutputHelper _output;

        public ResponseStatusExtensionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Failure Tests

        [Theory]
        [InlineData(ResponseStatus.Success, OpCode.Get)]
        [InlineData(ResponseStatus.RangeScanComplete, OpCode.Get)]
        [InlineData(ResponseStatus.RangeScanMore, OpCode.Get)]
        [InlineData(ResponseStatus.SubDocMultiPathFailure, OpCode.MultiLookup)]
        public void TestFailureFalse(ResponseStatus status, OpCode opCode)
        {
            Assert.False(status.Failure(opCode));
        }

        [Theory]
        [InlineData(ResponseStatus.KeyNotFound, OpCode.Add)]
        [InlineData(ResponseStatus.Locked, OpCode.Set)]
        [InlineData(ResponseStatus.Locked, OpCode.Unlock)]
        public void TestFailureTrue(ResponseStatus status, OpCode opCode)
        {
            Assert.True(status.Failure(opCode));
        }

        [Fact]
        public void TestKeyExists()
        {

            var allOpCodes = Enum.GetValues(typeof(OpCode));
            foreach (var withCas in new[] {true, false})
            foreach (OpCode opCode in allOpCodes)
            {
                var mockOp = new Mock<IOperation>(MockBehavior.Strict);
                mockOp.Setup(op => op.OpCode).Returns(opCode);
                mockOp.Setup(op => op.Cas).Returns(withCas ? 1234u : 0u);
                KeyValueErrorContext ctx = new();
                var ex = ResponseStatus.KeyExists.CreateException(ctx, mockOp.Object);

                try
                {
                    switch (opCode)
                    {
                        case OpCode.Add:
                            Assert.IsAssignableFrom<DocumentExistsException>(ex);
                            break;
                        case OpCode.SubMultiMutation:
                            // "If CAS is non-zero and KEY_EXISTS is returned by the server a CasMismatchException should be propagated."
                            // https://github.com/couchbaselabs/sdk-rfcs/blob/master/rfc/0053-sdk3-crud.md MutateIn -> Notes
                            if (withCas)
                            {
                                Assert.IsAssignableFrom<CasMismatchException>(ex);
                            }
                            else
                            {
                                Assert.IsAssignableFrom<DocumentExistsException>(ex);
                            }
                            break;
                        default:
                            Assert.IsAssignableFrom<CasMismatchException>(ex);
                            break;
                    }
                }
                catch (Exception)
                {
                    _output.WriteLine("Failure testing KeyExists with opCode={0} and withCas = {1}", opCode, withCas);
                    throw;
                }
            }
        }

        [Theory]
        [InlineData(OpCode.Set, false)]
        [InlineData(OpCode.Replace, false)]
        [InlineData(OpCode.Delete, false)]
        [InlineData(OpCode.Unlock, true)]
        public void TestUnlockCasMismatchSpecialCase(OpCode opcode, bool isCasMismatch)
        {
            // possibly a bug in the server?  Unlock returns Locked instead of CasMismatch.
            var mockOperation = new Mock<IOperation>();
            mockOperation.SetupGet(op => op.OpCode).Returns(opcode);
            var ex = ResponseStatus.Locked.CreateException(new KeyValueErrorContext(), mockOperation.Object);
            if (isCasMismatch)
            {
                Assert.True(ex is CasMismatchException);
            }
            else
            {
                Assert.False(ex is CasMismatchException);
                Assert.True(ex is DocumentLockedException);
            }
        }

        #endregion

        #region Sub-document multi-path failure mapping

        // A SubDocMultiPathFailure carries the failing spec's index + status in the body:
        // [index(1)][status(2, NBO)]. CreateException must map that inner status to the right
        // exception type for both lookups and mutations.
        [Theory]
        [InlineData(ResponseStatus.SubDocCannotInsert, typeof(ValueInvalidException))] // e.g. counter overflow
        [InlineData(ResponseStatus.SubDocDeltaRange, typeof(DeltaInvalidException))]
        [InlineData(ResponseStatus.SubDocNumRange, typeof(NumberTooBigException))]
        [InlineData(ResponseStatus.SubDocPathNotFound, typeof(PathNotFoundException))]
        [InlineData(ResponseStatus.SubDocPathExists, typeof(PathExistsException))]
        [InlineData(ResponseStatus.SubDocPathMismatch, typeof(PathMismatchException))]
        [InlineData(ResponseStatus.SubDocPathInvalid, typeof(PathInvalidException))]
        [InlineData(ResponseStatus.SubDocPathTooBig, typeof(PathTooBigException))]
        public void CreateException_SubDocMultiPathFailure_MapsInnerStatus(ResponseStatus innerStatus, Type expectedType)
        {
            // Arrange
            var body = new byte[3];
            body[0] = 0; // spec index
            ByteConverter.FromUInt16((ushort)innerStatus, body.AsSpan(1), true);

            var mockOp = new Mock<IOperation>();
            mockOp.SetupGet(op => op.OpCode).Returns(OpCode.SubMultiMutation);
            mockOp.Setup(op => op.ExtractBody())
                .Returns(new SlicedMemoryOwner<byte>(new FakeMemoryOwner<byte>(body)));

            // Act
            var ex = ResponseStatus.SubDocMultiPathFailure.CreateException(new KeyValueErrorContext(), mockOp.Object);

            // Assert
            Assert.IsType(expectedType, ex);
        }

        #endregion

        #region AUTH_STALE Tests

        [Fact]
        public void CreateException_AuthStale_ReturnsCouchbaseException()
        {
            // Arrange
            var mockOperation = new Mock<IOperation>();
            mockOperation.SetupGet(op => op.OpCode).Returns(OpCode.Get);
            mockOperation.SetupGet(op => op.Cas).Returns(0UL);
            var ctx = new KeyValueErrorContext();

            // Act
            var ex = ResponseStatus.AuthStale.CreateException(ctx, mockOperation.Object);

            // Assert
            Assert.IsType<CouchbaseException>(ex);
        }

        [Fact]
        public void CreateException_AuthStale_HasDescriptiveMessage()
        {
            // Arrange
            var mockOperation = new Mock<IOperation>();
            mockOperation.SetupGet(op => op.OpCode).Returns(OpCode.Get);
            var ctx = new KeyValueErrorContext();

            // Act
            var ex = ResponseStatus.AuthStale.CreateException(ctx, mockOperation.Object);

            // Assert
            Assert.Contains("AUTH_STALE", ex.Message);
            Assert.Contains("JWT", ex.Message);
        }

        /// <summary>
        /// The KeyValueErrorContext should be preserved on the exception so that users can
        /// see which bucket/key/operation was affected when the AUTH_STALE occurred. This
        /// is standard for all KV operation exceptions.
        /// </summary>
        [Fact]
        public void CreateException_AuthStale_HasKeyValueErrorContext()
        {
            // Arrange
            var mockOperation = new Mock<IOperation>();
            mockOperation.SetupGet(op => op.OpCode).Returns(OpCode.Get);
            mockOperation.SetupGet(op => op.Key).Returns("test-key");
            var ctx = new KeyValueErrorContext
            {
                BucketName = "test-bucket",
                DocumentKey = "test-key"
            };

            // Act
            var ex = ResponseStatus.AuthStale.CreateException(ctx, mockOperation.Object) as CouchbaseException;

            // Assert
            Assert.NotNull(ex);
            Assert.Equal(ctx, ex.Context);
            Assert.Equal("test-bucket", ((KeyValueErrorContext)ex.Context).BucketName);
            Assert.Equal("test-key", ((KeyValueErrorContext)ex.Context).DocumentKey);
        }

        [Theory]
        [InlineData(OpCode.Get)]
        [InlineData(OpCode.Set)]
        [InlineData(OpCode.Delete)]
        [InlineData(OpCode.Replace)]
        [InlineData(OpCode.Add)]
        [InlineData(OpCode.SubMultiMutation)]
        [InlineData(OpCode.MultiLookup)]
        public void CreateException_AuthStale_SameExceptionForAllOpCodes(OpCode opCode)
        {
            // Arrange
            var mockOperation = new Mock<IOperation>();
            mockOperation.SetupGet(op => op.OpCode).Returns(opCode);
            var ctx = new KeyValueErrorContext();

            // Act
            var ex = ResponseStatus.AuthStale.CreateException(ctx, mockOperation.Object);

            // Assert - AUTH_STALE should always return AuthenticationFailureException regardless of opcode
            Assert.IsType<CouchbaseException>(ex);
        }

        /// <summary>
        /// AUTH_STALE is not retriable. The connection's authentication state is permanently
        /// invalid until a new SASL auth is performed. The SDK's retry orchestrator should
        /// not attempt to retry the operation on the same connection.
        /// </summary>
        [Fact]
        public void AuthStale_IsNotRetriable()
        {
            // Arrange
            var mockOperation = new Mock<IOperation>();
            mockOperation.SetupGet(op => op.OpCode).Returns(OpCode.Get);

            // Act
            var isRetriable = ResponseStatus.AuthStale.IsRetriable(mockOperation.Object);

            // Assert - AUTH_STALE should NOT be retriable (connection needs to be dropped)
            Assert.False(isRetriable);
        }

        /// <summary>
        /// AUTH_STALE should resolve to NoRetry for the retry reason. This signals that the
        /// operation should fail immediately rather than being queued for retry.
        /// </summary>
        [Fact]
        public void AuthStale_ResolveRetryReason_ReturnsNoRetry()
        {
            // Act
            var reason = ResponseStatus.AuthStale.ResolveRetryReason();

            // Assert
            Assert.Equal(RetryReason.NoRetry, reason);
        }

        /// <summary>
        /// Sanity check: AUTH_STALE is status code 0x1f (31 decimal) per the protocol spec.
        /// </summary>
        [Fact]
        public void AuthStale_StatusValue_Is0x1f()
        {
            // Assert - Verify the enum value matches the protocol spec (0x1f = 31)
            Assert.Equal(0x1f, (int)ResponseStatus.AuthStale);
        }

        #endregion
    }
}
