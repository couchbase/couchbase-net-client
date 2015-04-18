using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Tests.Fakes
{
    public class FakeTranscoder : ITypeTranscoder
    {
         public FakeTranscoder()
            : this(new AutoByteConverter())
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

        public byte[] Encode<T>(T value, Couchbase.IO.Operations.Flags flags)
        {
            throw new NotImplementedException();
        }

        public T Decode<T>(ArraySegment<byte> buffer, int offset, int length, Couchbase.IO.Operations.Flags flags)
        {
            throw new NotImplementedException();
        }

        public T Decode<T>(byte[] buffer, int offset, int length, Couchbase.IO.Operations.Flags flags)
        {
            throw new NotImplementedException();
        }
    }
}
