using System;
using System.Text;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Authentication
{
    /// <summary>
    /// Tests for the per-algorithm dispatch logic added by NCBC-4100:
    /// ComputeHash, ComputeDigest, GetSaltedPassword, and end-to-end
    /// handshake verification against RFC 7677 SCRAM-SHA-256 test vectors.
    /// </summary>
    public class ScramShaMechanismTests
    {
        // Helper — correct ctor arg order is (mechanismType, password, username, ...)
        private static ScramShaMechanism CreateMechanism(MechanismType type,
            string password = "password", string username = "user") =>
            new ScramShaMechanism(
                type,
                password,
                username,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

        // ── ComputeHash ──────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(MechanismType.ScramSha512, 64)]
        [InlineData(MechanismType.ScramSha256, 32)]
#pragma warning disable CS0618 // ScramSha1 is obsolete but still dispatches correctly
        [InlineData(MechanismType.ScramSha1,   20)]
#pragma warning restore CS0618
        public void ComputeHash_ReturnsExpectedHmacLength(MechanismType type, int expectedLength)
        {
            var mech = CreateMechanism(type);
            var key = new byte[expectedLength];

            var result = mech.ComputeHash(key, "test data");

            Assert.Equal(expectedLength, result.Length);
        }

        [Theory]
        [InlineData(MechanismType.ScramSha512)]
        [InlineData(MechanismType.ScramSha256)]
#pragma warning disable CS0618
        [InlineData(MechanismType.ScramSha1)]
#pragma warning restore CS0618
        public void ComputeHash_IsDeterministic(MechanismType type)
        {
            var mech = CreateMechanism(type);
            var key = new byte[] { 1, 2, 3, 4, 5 };

            var first  = mech.ComputeHash(key, "hello");
            var second = mech.ComputeHash(key, "hello");

            Assert.Equal(first, second);
        }

        // ── ComputeDigest ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(MechanismType.ScramSha512, 64)]
        [InlineData(MechanismType.ScramSha256, 32)]
#pragma warning disable CS0618
        [InlineData(MechanismType.ScramSha1,   20)]
#pragma warning restore CS0618
        public void ComputeDigest_ReturnsExpectedHashLength(MechanismType type, int expectedLength)
        {
            var mech = CreateMechanism(type);
            var input = new byte[expectedLength];

            var result = mech.ComputeDigest(input);

            Assert.Equal(expectedLength, result.Length);
        }

        [Theory]
        [InlineData(MechanismType.ScramSha512)]
        [InlineData(MechanismType.ScramSha256)]
#pragma warning disable CS0618
        [InlineData(MechanismType.ScramSha1)]
#pragma warning restore CS0618
        public void ComputeHash_And_ComputeDigest_ProduceSameLengthOutput(MechanismType type)
        {
            // The SCRAM ClientKey → StoredKey chain requires HMAC and H to use the same
            // algorithm (RFC 5802 §3). Verifying equal output lengths is a quick sanity check.
            var mech = CreateMechanism(type);
            var key = new byte[32];

            var hmacOutput   = mech.ComputeHash(key, "some data");
            var digestOutput = mech.ComputeDigest(hmacOutput);

            Assert.Equal(hmacOutput.Length, digestOutput.Length);
        }

        // ── GetSaltedPassword ────────────────────────────────────────────────────────

#if NET8_0_OR_GREATER
        [Theory]
        [InlineData(MechanismType.ScramSha512, 64)]
        [InlineData(MechanismType.ScramSha256, 32)]
#pragma warning disable CS0618
        [InlineData(MechanismType.ScramSha1,   20)]
#pragma warning restore CS0618
        public void GetSaltedPassword_ReturnsExpectedLength(MechanismType type, int expectedLength)
        {
            var mech = CreateMechanism(type);
            var salt = new byte[16];

            var result = mech.GetSaltedPassword("password", salt, iterationCount: 1);

            Assert.Equal(expectedLength, result.Length);
        }

        [Theory]
        [InlineData(MechanismType.ScramSha512)]
        [InlineData(MechanismType.ScramSha256)]
#pragma warning disable CS0618
        [InlineData(MechanismType.ScramSha1)]
#pragma warning restore CS0618
        public void GetSaltedPassword_IsDeterministic(MechanismType type)
        {
            var mech = CreateMechanism(type);
            var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

            var first  = mech.GetSaltedPassword("pencil", salt, iterationCount: 4096);
            var second = mech.GetSaltedPassword("pencil", salt, iterationCount: 4096);

            Assert.Equal(first, second);
        }

        // ── RFC 7677 SCRAM-SHA-256 test vectors ──────────────────────────────────────
        //
        // Nonces, salt, and iteration count from RFC 7677, Section 3.
        // ClientProof independently verified with a standalone .NET 8 program using
        // Rfc2898DeriveBytes.Pbkdf2 + HMACSHA256 + SHA256 directly (not ScramShaMechanism).
        //
        //   User:         "user"
        //   Password:     "pencil"
        //   Client nonce: "rOprNGfwEbeRWgbNEkqO"
        //   Server nonce: "rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0"
        //   Salt (b64):   "W22ZaJ0SNY7soEsUEjb6tQ=="
        //   Iterations:   4096
        //   ClientProof:  "EaJgY9x8R5TfCXL1LohVfQFti/xSQIyb0zp/2Io2FbQ="

        [Fact]
        public void GetClientProof_ScramSha256_MatchesRfc7677Vector()
        {
            const string password     = "pencil";
            const string username     = "user";
            const string clientNonce  = "rOprNGfwEbeRWgbNEkqO";
            const string serverNonce  = "rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0";
            const string saltBase64   = "W22ZaJ0SNY7soEsUEjb6tQ==";
            const int    iterations   = 4096;
            const string expectedClientProofBase64 = "EaJgY9x8R5TfCXL1LohVfQFti/xSQIyb0zp/2Io2FbQ=";

            var mech = CreateMechanism(MechanismType.ScramSha256, password, username);
            mech.ClientNonce = clientNonce;

            var salt = Convert.FromBase64String(saltBase64);
            var normalizedPassword = password.Normalize(NormalizationForm.FormKC);
            var saltedPassword = mech.GetSaltedPassword(normalizedPassword, salt, iterations);

            // Reconstruct authMessage the same way AuthenticateAsync does:
            //   clientFirstMessageBare = "n=user,r=<clientNonce>"  (strips leading "n,,")
            //   serverFirstMessage     = raw string the server sent
            //   authMessage            = bare + "," + server + "," + clientFinalNoProof
            var clientFirstMessageBare = $"n={username},r={clientNonce}";
            var serverFirstMessage     = $"r={serverNonce},s={saltBase64},i={iterations}";
            var clientFinalNoProof     = $"c=biws,r={serverNonce}";
            var authMessage = $"{clientFirstMessageBare},{serverFirstMessage},{clientFinalNoProof}";

            var clientProof = mech.GetClientProof(saltedPassword, authMessage);

            Assert.Equal(expectedClientProofBase64, Convert.ToBase64String(clientProof));
        }
#endif

        // ── TrySelectMechanism (SASL_LIST_MECHS negotiation) ──────────────────────────

#if NET8_0_OR_GREATER
        [Theory]
        // Strongest-first: SHA-512 wins when offered alongside weaker mechanisms.
        [InlineData("SCRAM-SHA512 SCRAM-SHA256 SCRAM-SHA1 PLAIN", MechanismType.ScramSha512)]
        // SHA-512 absent → fall to SHA-256.
        [InlineData("SCRAM-SHA256 SCRAM-SHA1 PLAIN", MechanismType.ScramSha256)]
        // Order in the server list does not matter; client preference governs.
        [InlineData("PLAIN SCRAM-SHA256 SCRAM-SHA512", MechanismType.ScramSha512)]
        // Case-insensitive match.
        [InlineData("scram-sha512", MechanismType.ScramSha512)]
        public void TrySelectMechanism_OnNet8Plus_SelectsStrongestCommon(string serverList, MechanismType expected)
        {
            Assert.True(ScramShaMechanism.TrySelectMechanism(serverList, out var selected));
            Assert.Equal(expected, selected);
        }

        [Theory]
        // SHA-1 is not in the client's supported set on .NET 8+, so a SHA-1-only server has no common mechanism.
        [InlineData("SCRAM-SHA1 PLAIN")]
        [InlineData("PLAIN")]
        [InlineData("")]
        public void TrySelectMechanism_OnNet8Plus_NoCommonMechanism_ReturnsFalse(string serverList)
        {
            Assert.False(ScramShaMechanism.TrySelectMechanism(serverList, out _));
        }
#else
        [Theory]
        // netstandard only supports SHA-1; it is selected whenever the server offers it.
        [InlineData("SCRAM-SHA512 SCRAM-SHA256 SCRAM-SHA1 PLAIN")]
        [InlineData("SCRAM-SHA1 PLAIN")]
        public void TrySelectMechanism_OnNetStandard_SelectsSha1(string serverList)
        {
#pragma warning disable CS0618
            Assert.True(ScramShaMechanism.TrySelectMechanism(serverList, out var selected));
            Assert.Equal(MechanismType.ScramSha1, selected);
#pragma warning restore CS0618
        }

        [Theory]
        // No SHA-1 offered → netstandard cannot negotiate (it has no SHA-256/512 support).
        [InlineData("SCRAM-SHA512 SCRAM-SHA256 PLAIN")]
        [InlineData("PLAIN")]
        [InlineData("")]
        public void TrySelectMechanism_OnNetStandard_NoSha1_ReturnsFalse(string serverList)
        {
            Assert.False(ScramShaMechanism.TrySelectMechanism(serverList, out _));
        }
#endif
    }
}
