﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Tests.Fakes
{
    public class FakeTranscoder : ITypeTranscoder
    {
         public FakeTranscoder()
            : this(new DefaultConverter())
        {
        }

        public FakeTranscoder(IByteConverter converter)
            : this(converter, new DefaultSerializer())
        {
        }

        public FakeTranscoder(IByteConverter converter, ITypeSerializer serializer)
        {
            Serializer = serializer;
            Converter = converter;
        }

        public ITypeSerializer Serializer { get; set; }

        public IByteConverter Converter { get; set; }

        public Flags GetFormat<T>(T value)
        {
            throw new NotImplementedException();
        }

        public byte[] Encode<T>(T value, Flags flags, OperationCode opcode)
        {
            throw new NotImplementedException();
        }

        public T Decode<T>(ArraySegment<byte> buffer, int offset, int length, Flags flags, OperationCode opcode)
        {
            throw new NotImplementedException();
        }

        public T Decode<T>(byte[] buffer, int offset, int length, Flags flags, OperationCode opcode)
        {
            throw new NotImplementedException();
        }
    }
}
