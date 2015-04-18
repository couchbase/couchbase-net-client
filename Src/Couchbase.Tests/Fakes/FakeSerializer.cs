using System;
using Couchbase.Core.Serialization;

namespace Couchbase.Tests.Fakes
{
    public class FakeSerializer : ITypeSerializer
    {
        public T Deserialize<T>(byte[] buffer, int offset, int length)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(object obj)
        {
            throw new NotImplementedException();
        }


        public T Deserialize<T>(System.IO.Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
