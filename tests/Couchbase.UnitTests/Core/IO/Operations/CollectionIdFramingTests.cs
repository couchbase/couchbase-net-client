using System;
using System.Linq;
using System.Reflection;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    /// <summary>
    /// Whether a KV frame carries a leb128 collection ID prefix is a per-connection fact, fixed by
    /// what that connection negotiated in HELO. Deciding it from client-side state is NCBC-4146, and
    /// it corrupts keys in both directions: a prefix the server does not parse becomes part of the
    /// document ID, and a missing prefix makes the server read the first byte of the key as the
    /// collection.
    /// </summary>
    public class CollectionIdFramingTests
    {
        private const string Key = "user::1";
        private const uint Cid = 23;

        /// <summary>Exposes the protected framing method for a document operation.</summary>
        private class TestableGet : Get<byte[]>
        {
            public int Frame(Span<byte> buffer, bool collectionsEnabled) => WriteKey(buffer, collectionsEnabled);
        }

        /// <summary>
        /// Stands in for the connection and cluster level operations - HELO, SASL, SELECT_BUCKET and
        /// the rest are sealed or awkward to construct, and which of them opt out is pinned by
        /// <see cref="Only_connection_and_cluster_operations_opt_out_of_the_collection_id"/>. What is
        /// under test here is what opting out does to the frame.
        /// </summary>
        private class TestableConnectionOperation : Get<byte[]>
        {
            internal override bool RequiresCollectionId => false;

            public int Frame(Span<byte> buffer, bool collectionsEnabled) => WriteKey(buffer, collectionsEnabled);
        }

        [Fact]
        public void Collections_negotiated_writes_the_prefix()
        {
            var op = new TestableGet { Key = Key, Cid = Cid };
            var buffer = new byte[64];

            var length = op.Frame(buffer, collectionsEnabled: true);

            Assert.Equal(Key.Length + 1, length);
            Assert.Equal(Cid, buffer[0]);
            Assert.Equal(Key, System.Text.Encoding.UTF8.GetString(buffer, 1, Key.Length));
        }

        /// <summary>
        /// Polarity A of NCBC-4146: the SDK used to write a prefix whenever it happened to be holding
        /// a CID, even on a connection that never negotiated collections. The server then read the
        /// prefix byte as part of the document ID.
        /// </summary>
        [Fact]
        public void Collections_not_negotiated_writes_no_prefix_even_holding_a_cid()
        {
            var op = new TestableGet { Key = Key, Cid = Cid };
            var buffer = new byte[64];

            var length = op.Frame(buffer, collectionsEnabled: false);

            Assert.Equal(Key.Length, length);
            Assert.Equal(Key, System.Text.Encoding.UTF8.GetString(buffer, 0, Key.Length));
        }

        /// <summary>
        /// Polarity B, and the NCBC-4285 defect: no prefix on a connection that negotiated
        /// collections means the server eats the first byte of the key. Refuse to send it.
        /// </summary>
        [Fact]
        public void Collections_negotiated_without_a_cid_throws_rather_than_corrupting_the_key()
        {
            var op = new TestableGet { Key = Key };
            var buffer = new byte[64];

            var exception = Assert.Throws<CouchbaseException>(() => op.Frame(buffer, collectionsEnabled: true));

            Assert.Contains("no collection ID", exception.Message);
        }

        /// <summary>
        /// The default collection is CID 0, which is a real prefix and not the same frame as no
        /// prefix at all. Getting these two confused is the origin of the whole defect class.
        /// </summary>
        [Fact]
        public void Default_collection_writes_a_zero_prefix()
        {
            var op = new TestableGet { Key = Key, Cid = 0 };
            var buffer = new byte[64];

            var length = op.Frame(buffer, collectionsEnabled: true);

            Assert.Equal(Key.Length + 1, length);
            Assert.Equal(0, buffer[0]);
        }

        /// <summary>
        /// SELECT_BUCKET's key is a bucket name, and it is sent before any collection exists. It must
        /// neither be prefixed nor throw for the absent CID.
        /// </summary>
        [Fact]
        public void An_operation_that_addresses_no_document_is_never_prefixed()
        {
            var op = new TestableConnectionOperation { Key = "travel-sample" };
            var buffer = new byte[64];

            var length = op.Frame(buffer, collectionsEnabled: true);

            Assert.Equal("travel-sample".Length, length);
            Assert.Equal("travel-sample", System.Text.Encoding.UTF8.GetString(buffer, 0, length));
        }

        /// <summary>
        /// The key field carries the collection ID as well as the document ID, so a 250 byte key does
        /// not fit once a prefix is prepended. The Key setter only checks the document ID on its own,
        /// so this used to surface from OperationHeader.KeyLength as a bare
        /// ArgumentOutOfRangeException from inside the write path.
        /// </summary>
        [Fact]
        public void A_maximum_length_key_does_not_fit_once_prefixed()
        {
            var op = new TestableGet { Key = new string('k', 250), Cid = Cid };
            var buffer = new byte[512];

            var exception = Assert.Throws<InvalidArgumentException>(
                () => op.Frame(buffer, collectionsEnabled: true));

            Assert.Contains("collection ID prefix", exception.Message);
        }

        [Fact]
        public void A_key_that_leaves_room_for_the_prefix_is_accepted()
        {
            //Cid 23 is one leb128 byte, so 249 + 1 is exactly the budget.
            var op = new TestableGet { Key = new string('k', 249), Cid = Cid };
            var buffer = new byte[512];

            Assert.Equal(250, op.Frame(buffer, collectionsEnabled: true));
        }

        /// <summary>
        /// And the full 250 is still fine when no prefix is going to be written.
        /// </summary>
        [Fact]
        public void A_maximum_length_key_still_fits_without_collections()
        {
            var op = new TestableGet { Key = new string('k', 250), Cid = Cid };
            var buffer = new byte[512];

            Assert.Equal(250, op.Frame(buffer, collectionsEnabled: false));
        }

        /// <summary>
        /// This is the test that matters. RequiresCollectionId defaults to true because true is the
        /// safe answer: an operation added without a thought about framing then fails loudly instead
        /// of silently addressing the wrong collection. So the default is deliberately not pinned -
        /// only the opt-outs are, because an opt-out is the answer that can corrupt data.
        ///
        /// If this fails because you added an operation, decide which it is. Does it address a
        /// document? Then it needs a collection ID and you should not be here. Is it a connection or
        /// cluster level operation whose key is not a document ID? Then add it below.
        /// </summary>
        [Fact]
        public void Only_connection_and_cluster_operations_opt_out_of_the_collection_id()
        {
            string[] expected =
            [
                "ClusterMapChangeNotification", // a server push, no document key
                "Config",                       // cluster map request
                "GetCid",                       // the key is a collection name - this resolves CIDs
                "GetErrorMap",                  // writes no key
                "GetManifest",                  // manifest request
                "GetSid",                       // the key is a scope name
                "Hello",                        // the key is a connection identifier
                "Noop",                         // writes no key
                "RangeScanCancel",              // writes no key, identified by scan UUID
                "RangeScanContinue",            // writes no key, identified by scan UUID
                "RangeScanCreate",              // writes no key, collection travels in the body
                "SaslList",                     // mechanism negotiation
                "SaslStart",                    // authentication, before any collection exists
                "SaslStep",                     // authentication, before any collection exists
                "SelectBucket"                  // the key is the bucket name
            ];

            var actual = typeof(OperationBase).Assembly.GetTypes()
                .Where(type => typeof(OperationBase).IsAssignableFrom(type) && type != typeof(OperationBase))
                .Where(type => type.GetProperty(nameof(OperationBase.RequiresCollectionId),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly) is not null)
                .Select(type => type.Name)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// SASL is the trap worth a test of its own: HELO completes before authentication, so by the
        /// time SaslStep runs the connection has already negotiated collections while no collection
        /// exists. An exemption list drafted by hand had SaslStart and SaslList but not SaslStep,
        /// which would have failed authentication on every non-TLS connection.
        /// </summary>
        [Fact]
        public void Every_sasl_operation_opts_out()
        {
            Assert.False(new SaslStart().RequiresCollectionId);
            Assert.False(new SaslStep().RequiresCollectionId);
            Assert.False(new SaslList().RequiresCollectionId);
        }

        [Fact]
        public void A_document_operation_requires_a_collection_id_by_default()
        {
            Assert.True(new Get<byte[]>().RequiresCollectionId);
            Assert.True(new Set<byte[]>("bucket", Key).RequiresCollectionId);
            Assert.True(new Delete().RequiresCollectionId);
        }
    }
}
