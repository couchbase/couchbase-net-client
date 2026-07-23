#nullable enable
using System;
using System.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Internal
{
    /// <summary>
    /// A transcoder decorator that pins the flags written to the document, delegating the actual
    /// byte encoding/decoding to an inner transcoder.
    /// <para>
    /// On a .NET mutation the persisted flags are always <c>Transcoder.GetFormat(content)</c>
    /// (see <c>OperationBase&lt;T&gt;.WriteExtras</c>) — there is no per-operation flags override,
    /// which is why <c>InsertOptions</c> exposes none. When committing content we did not stage
    /// (lost-transaction cleanup) or via the legacy insert path, we must persist the user flags
    /// recorded at staging time (<c>txn.aux.uf</c>) rather than flags re-derived from the content.
    /// This wrapper makes <see cref="GetFormat{T}"/> report those staged flags so they land on the
    /// document, while <see cref="Encode{T}"/>/<see cref="Decode{T}"/> behave exactly as the inner
    /// transcoder. It is the .NET analogue of Java passing <c>stagedUserFlags</c> straight to the
    /// insert request.
    /// </para>
    /// </summary>
    internal sealed class FixedFlagsTranscoder : ITypeTranscoder
    {
        private readonly ITypeTranscoder _inner;
        private readonly Flags _flags;

        public FixedFlagsTranscoder(ITypeTranscoder inner, Flags flags)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _flags = flags;
        }

        /// <summary>Always reports the fixed (staged) flags, ignoring the content's runtime type.</summary>
        public Flags GetFormat<T>(T value) => _flags;

        public void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode) =>
            _inner.Encode(stream, value, flags, opcode);

        public T? Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode) =>
            _inner.Decode<T>(buffer, flags, opcode);

        public ITypeSerializer? Serializer
        {
            get => _inner.Serializer;
            set => _inner.Serializer = value;
        }
    }
}
