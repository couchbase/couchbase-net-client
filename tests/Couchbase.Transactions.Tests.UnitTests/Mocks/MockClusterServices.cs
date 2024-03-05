using System;
using System.Collections.Generic;
using System.IO;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;

namespace Couchbase.Transactions.Tests.UnitTests.Mocks
{
    internal class MockClusterServices : IServiceProvider
    {
        private Dictionary<Type, object> _services = new Dictionary<Type, object>()
        {
            {typeof(IRedactor), new MockRedactor()},
            {typeof(ITypeTranscoder), new MockTranscoder()},
            {typeof(ILoggerFactory), new MockLoggerFactory()},
            {typeof(IRequestTracer), new NoopRequestTracer()},
        };

        public object GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service;
            }
            else
            {
                throw new NotSupportedException($"{serviceType} has not been registered with mock DI.");
            }
        }

        internal void RegisterSingleton(Type serviceType, object singleton)
        {
            _services[serviceType] = singleton;
        }
    }

    internal class MockTranscoder : ITypeTranscoder
    {
        public Flags GetFormat<T>(T value) => new Flags() {Compression = Couchbase.Core.IO.Operations.Compression.None, DataFormat = DataFormat.Json, TypeCode = TypeCode.Object };

        public void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode) => Serializer.Serialize(stream, value);

        public T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode) => Serializer.Deserialize<T>(buffer);

        public ITypeSerializer Serializer { get; set; } = new DefaultSerializer();
    }

    internal class MockRedactor : IRedactor
    {
        public object UserData(object message) => $"MOCK_USER_REDACTED({message})";

        public object MetaData(object message) => $"MOCK_META_REDACTED({message})";

        public object SystemData(object message) => $"MOCK_SYSTEM_REDACTED({message})";
    }

    internal class MockLoggerFactory : ILoggerFactory
    {
        public void Dispose()
        {

        }

        public ILogger CreateLogger(string categoryName) => new Mock<ILogger>().Object;

        public void AddProvider(ILoggerProvider provider)
        {

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
