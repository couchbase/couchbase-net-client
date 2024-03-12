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
using Couchbase.Core.IO.Operations;
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
    }
}
