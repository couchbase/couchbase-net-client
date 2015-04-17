using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Tests.Fakes
{
    public class FakeTranscoder : ITypeTranscoder
    {
        public FakeTranscoder(IByteConverter converter)
            : this(
                converter, new JsonSerializerSettings(),
                new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()})
        {
        }

        public FakeTranscoder(IByteConverter converter, JsonSerializerSettings incomingSerializerSettings,
            JsonSerializerSettings outgoingSerializerSettings)
        {
        }

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
