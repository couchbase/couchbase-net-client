using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Authentication
{
    /// <summary>
    /// Runs the full SCRAM exchange against a scripted server, to check that the client authenticates the
    /// server as RFC 5802 requires (NCBC-4339): the server-final signature must match, the server nonce must
    /// extend the client nonce, and an error in the server-final fails the exchange.
    /// </summary>
    public class ScramShaExchangeTests
    {
        public class Vector
        {
            public MechanismType Mechanism { get; set; }
            public string ClientNonce { get; set; }
            public string ServerNonce { get; set; }
            public string Salt { get; set; }
            public string ClientProof { get; set; }
            public string ServerSignature { get; set; }

            public string ServerFirst => $"r={ServerNonce},s={Salt},i=4096";

            public override string ToString() => Mechanism.ToString();
        }

        // RFC 5802 §5. SHA-1 runs on every TFM: only SaslMechanismFactory rejects it on .NET 8+, so the
        // net48 leg (netstandard2.0 asset) is covered too.
        private static readonly Vector Rfc5802Sha1 = new()
        {
#pragma warning disable CS0618
            Mechanism = MechanismType.ScramSha1,
#pragma warning restore CS0618
            ClientNonce = "fyko+d2lbbFgONRv9qkxdawL",
            ServerNonce = "fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j",
            Salt = "QSXCR+Q6sek8bf92",
            ClientProof = "v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=",
            ServerSignature = "rmF9pqV8S7suAoZWja4dJRkFsKQ="
        };

        // RFC 7677 §3. SHA-256 PBKDF2 needs .NET 8+.
        private static readonly Vector Rfc7677Sha256 = new()
        {
            Mechanism = MechanismType.ScramSha256,
            ClientNonce = "rOprNGfwEbeRWgbNEkqO",
            ServerNonce = "rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0",
            Salt = "W22ZaJ0SNY7soEsUEjb6gQ==",
            ClientProof = "dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=",
            ServerSignature = "6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4="
        };

        public static IEnumerable<object[]> Vectors()
        {
            yield return new object[] { Rfc5802Sha1 };
#if NET8_0_OR_GREATER
            yield return new object[] { Rfc7677Sha256 };
#endif
        }

        [Theory]
        [MemberData(nameof(Vectors))]
        public async Task Exchange_With_Correct_Server_Signature_Succeeds(Vector vector)
        {
            var server = new ScriptedServer(vector.ServerFirst, "v=" + vector.ServerSignature);

            await Mechanism(vector).AuthenticateAsync(server.Connection, CancellationToken.None);

            Assert.Equal($"c=biws,r={vector.ServerNonce},p={vector.ClientProof}", server.ClientFinal);
        }

        [Theory]
        [MemberData(nameof(Vectors))]
        public async Task Forged_Server_Signature_Is_Rejected(Vector vector)
        {
            var forged = Convert.ToBase64String(new byte[Convert.FromBase64String(vector.ServerSignature).Length]);
            var server = new ScriptedServer(vector.ServerFirst, "v=" + forged);

            await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(vector).AuthenticateAsync(server.Connection, CancellationToken.None));
        }

        [Theory]
        [MemberData(nameof(Vectors))]
        public async Task Truncated_Server_Signature_Is_Rejected(Vector vector)
        {
            var truncated = Convert.ToBase64String(Convert.FromBase64String(vector.ServerSignature), 0, 8);
            var server = new ScriptedServer(vector.ServerFirst, "v=" + truncated);

            await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(vector).AuthenticateAsync(server.Connection, CancellationToken.None));
        }

        [Theory]
        [InlineData("")]
        [InlineData("e=invalid-proof")]
        [InlineData("v=not base64!")]
        [InlineData("x=extension-only")]
        public async Task Server_Final_Without_A_Valid_Signature_Is_Rejected(string serverFinal)
        {
            var server = new ScriptedServer(Rfc5802Sha1.ServerFirst, serverFinal);

            await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(Rfc5802Sha1).AuthenticateAsync(server.Connection, CancellationToken.None));
        }

        [Fact]
        public async Task Server_Error_Is_Reported()
        {
            var server = new ScriptedServer(Rfc5802Sha1.ServerFirst, "e=invalid-proof");

            var ex = await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(Rfc5802Sha1).AuthenticateAsync(server.Connection, CancellationToken.None));

            Assert.Contains("invalid-proof", ex.Message);
        }

        [Theory]
        // Control characters could forge log lines.
        [InlineData("ok\n[INFO] forged log line")]
        // Longer than any RFC 5802 error value.
        [InlineData("a-very-long-error-value-that-no-scram-server-would-ever-send-as-its-e-attribute")]
        public async Task Unrecognized_Server_Error_Is_Not_Repeated(string error)
        {
            var server = new ScriptedServer(Rfc5802Sha1.ServerFirst, "e=" + error);

            var ex = await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(Rfc5802Sha1).AuthenticateAsync(server.Connection, CancellationToken.None));

            Assert.DoesNotContain(error, ex.Message);
            Assert.Contains("an unrecognized error", ex.Message);
        }

        [Fact]
        public async Task Server_Final_With_A_Non_Letter_Attribute_Is_Rejected()
        {
            var server = new ScriptedServer(Rfc5802Sha1.ServerFirst, "v=" + Rfc5802Sha1.ServerSignature + ",1=x");

            await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(Rfc5802Sha1).AuthenticateAsync(server.Connection, CancellationToken.None));
        }

        [Theory]
        // Does not start with the client nonce.
        [InlineData("r=ATTACKER-CHOSEN-NONCE,s=QSXCR+Q6sek8bf92,i=4096")]
        // Equal to the client nonce: the server added nothing.
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL,s=QSXCR+Q6sek8bf92,i=4096")]
        // Prefix matches only case-insensitively.
        [InlineData("r=FYKO+D2LBBFGONRV9QKXDAWL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096")]
        // Missing nonce, salt or iteration count.
        [InlineData("s=QSXCR+Q6sek8bf92,i=4096")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,i=4096")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92")]
        // Malformed salt or iteration count.
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=not base64!,i=4096")]
        // Salt shorter than 8 bytes (0, 3 and 7 bytes).
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=,i=4096")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=AAAA,i=4096")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=AAAAAAAAAA==,i=4096")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=0")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=-1")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=lots")]
        // Above Couchbase Server's maximum of 10,000,000.
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=10000001")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=2147483647")]
        // Repeated or malformed attribute.
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,r=other,s=QSXCR+Q6sek8bf92,i=4096")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,,s=QSXCR+Q6sek8bf92,i=4096")]
        // Attribute name that is not a letter.
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096,1=x")]
        [InlineData("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096, =x")]
        // Mandatory extension the client does not support.
        [InlineData("m=ext,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096")]
        public async Task Invalid_Server_First_Is_Rejected_Before_The_Proof_Is_Sent(string serverFirst)
        {
            var server = new ScriptedServer(serverFirst, "v=" + Rfc5802Sha1.ServerSignature);

            await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
                Mechanism(Rfc5802Sha1).AuthenticateAsync(server.Connection, CancellationToken.None));

            Assert.Null(server.ClientFinal);
        }

        [Fact]
        public void Unknown_Optional_Extension_In_Server_First_Is_Ignored()
        {
            var mech = Mechanism(Rfc5802Sha1);

            var (nonce, _, iterationCount) = mech.ParseServerFirst(Rfc5802Sha1.ServerFirst + ",x=ignored");

            Assert.Equal(Rfc5802Sha1.ServerNonce, nonce);
            Assert.Equal(4096, iterationCount);
        }

        [Fact]
        public void Server_Maximum_Iteration_Count_Is_Accepted()
        {
            var mech = Mechanism(Rfc5802Sha1);

            var (_, _, iterationCount) = mech.ParseServerFirst(
                "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=10000000");

            Assert.Equal(10_000_000, iterationCount);
        }

        [Fact]
        public void Eight_Byte_Salt_Is_Accepted()
        {
            var mech = Mechanism(Rfc5802Sha1);

            var (_, salt, _) = mech.ParseServerFirst(
                "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=AAAAAAAAAAA=,i=4096");

            Assert.Equal(8, salt.Length);
        }

        private static ScramShaMechanism Mechanism(Vector vector) =>
            new(vector.Mechanism, "pencil", "user",
                new Mock<ILogger<ScramShaMechanism>>().Object,
                NoopRequestTracer.Instance,
                new OperationConfigurator(new LegacyTranscoder(),
                    Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()))
            {
                ClientNonce = vector.ClientNonce
            };

        /// <summary>
        /// Completes each SASL operation inside SendAsync: SASL_START gets the server-first-message with
        /// AuthenticationContinue, SASL_STEP gets the server-final-message with Success.
        /// </summary>
        private sealed class ScriptedServer
        {
            public ScriptedServer(string serverFirst, string serverFinal)
            {
                var connection = new Mock<IConnection>();
                connection.SetupGet(c => c.ServerFeatures).Returns(ServerFeatureSet.Empty);
                connection.SetupProperty(c => c.EndpointState);
                connection
                    .Setup(c => c.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IOperation>(),
                        It.IsAny<CancellationToken>()))
                    .Returns((ReadOnlyMemory<byte> _, IOperation op, CancellationToken _) =>
                    {
                        if (op is SaslStep step)
                        {
                            ClientFinal = step.Content;
                            op.HandleOperationCompleted(Response(OpCode.SaslStep, ResponseStatus.Success, serverFinal));
                        }
                        else
                        {
                            op.HandleOperationCompleted(Response(OpCode.SaslStart,
                                ResponseStatus.AuthenticationContinue, serverFirst));
                        }

                        return default;
                    });
                Connection = connection.Object;
            }

            public IConnection Connection { get; }

            /// <summary>
            /// The client-final-message, or null if the client never sent one.
            /// </summary>
            public string ClientFinal { get; private set; }

            private static SlicedMemoryOwner<byte> Response(OpCode opCode, ResponseStatus status, string body)
            {
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var response = MemoryPool<byte>.Shared.RentAndSlice(HeaderOffsets.HeaderLength + bodyBytes.Length);
                var span = response.Memory.Span;
                span.Clear();
                span[HeaderOffsets.Magic] = (byte) Magic.ClientResponse;
                span[HeaderOffsets.Opcode] = (byte) opCode;
                BinaryPrimitives.WriteUInt16BigEndian(span.Slice(HeaderOffsets.Status), (ushort) status);
                BinaryPrimitives.WriteUInt32BigEndian(span.Slice(HeaderOffsets.BodyLength), (uint) bodyBytes.Length);
                bodyBytes.CopyTo(span.Slice(HeaderOffsets.HeaderLength));
                return response;
            }
        }
    }
}
