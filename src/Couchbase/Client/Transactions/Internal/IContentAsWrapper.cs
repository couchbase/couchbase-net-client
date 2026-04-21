#nullable enable
using System;
using System.IO;
using System.Reflection.Emit;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using OpCode = Couchbase.Core.IO.Operations.OpCode;

namespace Couchbase.Client.Transactions.Internal
{
    /// <summary>
    /// An interface for deferring ContentAs calls to their original source to avoid byte[]/json/string conversion in the middle.
    /// </summary>
    internal interface IContentAsWrapper
    {
        T? ContentAs<T>();
        ITypeTranscoder Transcoder { get; }
        Flags Flags { get; }

        bool IsBinary { get; }
    }

    internal class TranscodedContentWrapper : IContentAsWrapper
    {
        private readonly ReadOnlyMemory<byte> _encodedContent;
        public Flags Flags { get; }

        public bool IsBinary { get; }

        public ITypeTranscoder Transcoder { get; }

        public TranscodedContentWrapper(object? originalContent, ITypeTranscoder? transcoder = null)
        {
            // Callers staging user content MUST pass the cluster-derived user transcoder. The
            // fallback below uses the default (camelCase) serializer, which would silently re-encode
            // content with the wrong property casing (CBSE-22995); it exists only for placeholders /
            // empty content where casing is irrelevant.
            Transcoder = transcoder ?? new JsonTranscoder();
            var stream = new MemoryStream();
            switch (originalContent)
            {
                case ReadOnlyMemory<byte> roMemory:
                    Flags = Transcoder.GetFormat(roMemory);
                    Transcoder.Encode(stream, roMemory, Flags, OpCode.Get);
                    break;
                case Memory<byte> memory:
                    Flags = Transcoder.GetFormat(memory);
                    Transcoder.Encode(stream, memory, Flags, OpCode.Get);
                    break;
                case byte[] bytes:
                    Flags = Transcoder.GetFormat(bytes);
                    Transcoder.Encode(stream, bytes, Flags, OpCode.Get);
                    break;
                default:
                    Flags = Transcoder.GetFormat(originalContent);
                    Transcoder.Encode(stream, originalContent, Flags, OpCode.Get);
                    break;
            }
            IsBinary = Flags.DataFormat == DataFormat.Binary;
            _encodedContent = stream.ToArray();
        }

        public T? ContentAs<T>()
        {
            if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)_encodedContent.ToArray();
            }
            if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            {
                return (T)(object)_encodedContent;
            }
            if (typeof(T) == typeof(Memory<byte>))
            {
                var copy = new byte[_encodedContent.Length];
                _encodedContent.CopyTo(copy);
                return (T)(object)new Memory<byte>(copy);
            }
            return Transcoder.Decode<T>(_encodedContent, Flags, OpCode.Set);
        }
    }

    internal class LookupInContentAsWrapper : IContentAsWrapper
    {
        private readonly ILookupInResult _lookupInResult;
        private readonly int _specIndex;
        public ITypeTranscoder Transcoder { get; }

        public Flags Flags { get; init; }

        public bool IsBinary { get; init; }

        public LookupInContentAsWrapper(ILookupInResult lookupInResult, int specIndex, ITypeTranscoder? transcoder = null)
        {
            _lookupInResult = lookupInResult;
            if (lookupInResult is not ILookupInResultInternal res)
            {
                throw new InvalidArgumentException("lookupInResult is not a LookupInResult");
            }
            // NOTE: this Flags isn't necessarily what we want to use for the flags if this specIndex
            // becomes the document body.
            Flags = res.Flags;
            _specIndex = specIndex;
            IsBinary = (res.Specs[specIndex].PathFlags & SubdocPathFlags.BinaryValue) != 0;
            Transcoder = transcoder ?? new JsonTranscoder();
        }

        public T? ContentAs<T>()
        {
            if (_lookupInResult is not ILookupInResultInternal res)
            {
                throw new InvalidArgumentException("lookupInResult is not a LookupInResult");
            }
            if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)res.Specs[_specIndex].Bytes.ToArray();
            }

            if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            {
                return (T)(object)res.Specs[_specIndex].Bytes;
            }

            if (typeof(T) == typeof(Memory<byte>))
            {
                return  (T)(object)res.Specs[_specIndex].Bytes.ToArray();
            }

            // Use the wrapper's own Transcoder (the user data transcoder) to decode,
            // NOT the LookupInResult's serializer (which is the metadata serializer).
            // The LookupIn was performed with MetadataTranscoder for xattr access,
            // but user document content must be decoded with the user's serializer.
            return Transcoder.Decode<T>(res.Specs[_specIndex].Bytes, Flags, OpCode.Get);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
