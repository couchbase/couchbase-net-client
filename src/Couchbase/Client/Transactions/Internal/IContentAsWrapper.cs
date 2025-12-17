#nullable enable
using System;
using System.IO;
using System.Reflection.Emit;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Newtonsoft.Json.Linq;
using OpCode = Couchbase.Core.IO.Operations.OpCode;

namespace Couchbase.Client.Transactions.Internal
{
    /// <summary>
    /// An interface for deferring ContentAs calls to their original source to avoid byte[]/json/string conversion in the middle.
    /// </summary>
    internal interface IContentAsWrapper
    {
        T? ContentAs<T>();
        ITypeTranscoder Transcoder { get; set; }
        Flags Flags { get; }

        bool IsBinary { get; }
    }

    internal class JObjectContentWrapper : IContentAsWrapper
    {
        private readonly ReadOnlyMemory<byte> _originalContent;
        public Flags Flags { get; }

        public  bool IsBinary { get; }

        public ITypeTranscoder Transcoder { get; set; }

        public JObjectContentWrapper(object? originalContent, ITypeTranscoder? transcoder = null)
        {
            Transcoder = transcoder ?? new JsonTranscoder();
            var stream = new MemoryStream();
            switch (originalContent)
            {
                case ReadOnlyMemory<byte> roMemory:
                    _originalContent = roMemory;
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
            // now, store the encoded content
            _originalContent = stream.ToArray();
        }

        public T? ContentAs<T>()
        {
            if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)_originalContent.ToArray();
            }
            if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            {
                return (T)(object)_originalContent;
            }
            if (typeof(T) == typeof(Memory<byte>))
            {
                var copy = new byte[_originalContent.Length];
                _originalContent.CopyTo(copy);
                return (T)(object)new Memory<byte>(copy);
            }
            return Transcoder.Decode<T>(_originalContent, Flags, OpCode.Set);
        }
    }

    internal class LookupInContentAsWrapper : IContentAsWrapper
    {
        private readonly ILookupInResult _lookupInResult;
        private readonly int _specIndex;
        public ITypeTranscoder Transcoder { get; set; }

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
            Transcoder  = transcoder ?? new JsonTranscoder();
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
            return _lookupInResult.ContentAs<T>(_specIndex);
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
