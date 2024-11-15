using System;
using System.Linq;
using System.Text;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class OperationBuilderTests
    {
        #region Write

        public static readonly TheoryData<int, int> WriteSizes = new()
        {
            {1, 1},
            {1024, 1},
            {32768, 1024},
            {128 * 1024, 16000},
        };

        [Theory]
        [MemberData(nameof(WriteSizes))]
        public void Write_Array(int bodySize, int writeSize)
        {
            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                builder.Write(buffer, 0, Math.Min(buffer.Length, bodySize - written));
            }

            WriteHeader(builder);

            // Assert

            AssertBuilder(builder, "key", bodySize, buffer);
        }

        [Theory]
        [MemberData(nameof(WriteSizes))]
        public void Write_Span(int bodySize, int writeSize)
        {
            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                builder.Write(buffer.AsSpan(0, Math.Min(buffer.Length, bodySize - written)));
            }

            WriteHeader(builder);

            // Assert

            AssertBuilder(builder, "key", bodySize, buffer);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1024)]
        [InlineData(32768)]
        public void WriteByte(int bodySize)
        {
            // Arrange

            using var builder = new OperationBuilder();

            // Act

            WriteKey(builder, "key");

            for (int written = 0; written < bodySize; written++)
            {
                builder.WriteByte((byte)(written & 0xff));
            }

            WriteHeader(builder);

            // Assert

            AssertBuilder(builder, "key", bodySize, [0]);
        }

        [Theory]
        [MemberData(nameof(WriteSizes))]
        public void Write_GetMemoryAndAdvance(int bodySize, int writeSize)
        {
            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                var thisWriteSize = Math.Min(buffer.Length, bodySize - written);
                var memory = builder.GetMemory(thisWriteSize);
                buffer.AsSpan(0, thisWriteSize).CopyTo(memory.Span);
                builder.Advance(thisWriteSize);
            }

            WriteHeader(builder);

            // Assert

            AssertBuilder(builder, "key", bodySize, buffer);
        }

        [Theory]
        [MemberData(nameof(WriteSizes))]
        public void Write_GetSpanAndAdvance(int bodySize, int writeSize)
        {
            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                var thisWriteSize = Math.Min(buffer.Length, bodySize - written);
                var span = builder.GetSpan(thisWriteSize);
                buffer.AsSpan(0, thisWriteSize).CopyTo(span);
                builder.Advance(thisWriteSize);
            }

            WriteHeader(builder);

            // Assert

            AssertBuilder(builder, "key", bodySize, buffer);
        }

        #endregion

        #region Write Operation Specs

        public static readonly TheoryData<int, int, int> OperationSpecSizes = new()
        {
            {4, 0, 0},
            {4, 1, 1},
            {3, 0, 0},
            {3, 1024, 1},
            {2, 0, 0},
            {2, 32768, 1024},
            {1, 0, 0},
            {1, 128 * 1024, 16000},
        };

        [Theory]
        [MemberData(nameof(OperationSpecSizes))]
        public void Write_OperationSpec(int numSpecs, int fragmentSize, int writeSize)
        {
            // fragmentSize == 0 means a LookupIn operation, which has no fragment

            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int i = 0; i < numSpecs; i++)
            {
                builder.BeginOperationSpec(fragmentSize > 0);

                var path = $"{i}";
                var pathArray = Encoding.UTF8.GetBytes(path);
                builder.Write(pathArray, 0, pathArray.Length);

                if (fragmentSize > 0)
                {
                    builder.AdvanceToSegment(OperationSegment.OperationSpecFragment);

                    for (int written = 0; written < fragmentSize; written += buffer.Length)
                    {
                        var thisWriteSize = Math.Min(buffer.Length, fragmentSize - written);
                        builder.Write(buffer, 0, thisWriteSize);
                    }
                }

                builder.CompleteOperationSpec(fragmentSize > 0 ? MutateInSpec.Replace(path, i) : LookupInSpec.Get(path));
            }
        }

        [Theory]
        [MemberData(nameof(OperationSpecSizes))]
        public void Write_OperationSpec_Span(int numSpecs, int fragmentSize, int writeSize)
        {
            // fragmentSize == 0 means a LookupIn operation, which has no fragment

            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            Span<byte> pathBuffer = stackalloc byte[4];
            for (int i = 0; i < numSpecs; i++)
            {
                builder.BeginOperationSpec(fragmentSize > 0);

                var path = $"{i}";
                var pathLength = ByteConverter.FromString(path, pathBuffer);
                builder.Write(pathBuffer.Slice(0, pathLength));

                if (fragmentSize > 0)
                {
                    builder.AdvanceToSegment(OperationSegment.OperationSpecFragment);

                    for (int written = 0; written < fragmentSize; written += buffer.Length)
                    {
                        var thisWriteSize = Math.Min(buffer.Length, fragmentSize - written);
                        builder.Write(buffer.AsSpan(0, thisWriteSize));
                    }
                }

                builder.CompleteOperationSpec(fragmentSize > 0 ? MutateInSpec.Replace(path, i) : LookupInSpec.Get(path));
            }
        }

        [Theory]
        [MemberData(nameof(OperationSpecSizes))]
        public void Write_OperationSpec_GetMemoryAndAdvance(int numSpecs, int fragmentSize, int writeSize)
        {
            // fragmentSize == 0 means a LookupIn operation, which has no fragment

            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int i = 0; i < numSpecs; i++)
            {
                builder.BeginOperationSpec(fragmentSize > 0);

                var path = $"{i}";
                var pathLength = ByteConverter.GetStringByteCount(path);
                var span = builder.GetMemory(pathLength).Span;
                ByteConverter.FromString(path, span);
                builder.Advance(pathLength);

                if (fragmentSize > 0)
                {
                    builder.AdvanceToSegment(OperationSegment.OperationSpecFragment);

                    for (int written = 0; written < fragmentSize; written += buffer.Length)
                    {
                        var thisWriteSize = Math.Min(buffer.Length, fragmentSize - written);
                        span = builder.GetMemory(thisWriteSize).Span;
                        buffer.AsSpan(0, thisWriteSize).CopyTo(span);
                        builder.Advance(thisWriteSize);
                    }
                }

                builder.CompleteOperationSpec(fragmentSize > 0 ? MutateInSpec.Replace(path, i) : LookupInSpec.Get(path));
            }

            WriteHeader(builder);

            // Assert

            AssertMultiBuilder(builder, "key", numSpecs, fragmentSize, buffer);
        }

        [Theory]
        [MemberData(nameof(OperationSpecSizes))]
        public void Write_OperationSpec_GetSpanAndAdvance(int numSpecs, int fragmentSize, int writeSize)
        {
            // fragmentSize == 0 means a LookupIn operation, which has no fragment

            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, writeSize).Select(i => (byte)(i & 0xff)).ToArray();

            // Act

            WriteKey(builder, "key");

            for (int i = 0; i < numSpecs; i++)
            {
                builder.BeginOperationSpec(fragmentSize > 0);

                var path = $"{i}";
                var pathLength = ByteConverter.GetStringByteCount(path);
                var span = builder.GetSpan(pathLength);
                ByteConverter.FromString(path, span);
                builder.Advance(pathLength);

                if (fragmentSize > 0)
                {
                    builder.AdvanceToSegment(OperationSegment.OperationSpecFragment);

                    for (int written = 0; written < fragmentSize; written += buffer.Length)
                    {
                        var thisWriteSize = Math.Min(buffer.Length, fragmentSize - written);
                        span = builder.GetSpan(thisWriteSize);
                        buffer.AsSpan(0, thisWriteSize).CopyTo(span);
                        builder.Advance(thisWriteSize);
                    }
                }

                builder.CompleteOperationSpec(fragmentSize > 0 ? MutateInSpec.Replace(path, i) : LookupInSpec.Get(path));
            }

            WriteHeader(builder);

            // Assert

            AssertMultiBuilder(builder, "key", numSpecs, fragmentSize, buffer);
        }

        #endregion

        #region Reset

        [Fact]
        public void Reset_IsReusable()
        {
            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, 128).Select(i => (byte)(i & 0xff)).ToArray();

            WriteKey(builder, "key");

            const int bodySize = 1024;
            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                var thisWriteSize = Math.Min(buffer.Length, bodySize - written);
                var span = builder.GetSpan(thisWriteSize);
                buffer.AsSpan(0, thisWriteSize).CopyTo(span);
                builder.Advance(thisWriteSize);
            }

            WriteHeader(builder);

            // Act

            builder.Reset();

            // Assert

            buffer = Enumerable.Range(64, 128).Select(i => (byte)(i & 0xff)).ToArray();

            WriteKey(builder, "key");

            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                var thisWriteSize = Math.Min(buffer.Length, bodySize - written);
                var span = builder.GetSpan(thisWriteSize);
                buffer.AsSpan(0, thisWriteSize).CopyTo(span);
                builder.Advance(thisWriteSize);
            }

            WriteHeader(builder);

            AssertBuilder(builder, "key", bodySize, buffer);
        }

        [Fact]
        public void Reset_ClearsPreviousBuffer()
        {
            // Arrange

            using var builder = new OperationBuilder();

            var buffer = Enumerable.Range(0, 128).Select(i => (byte)(i & 0xff)).ToArray();

            WriteKey(builder, "key");

            const int bodySize = 1024;
            for (int written = 0; written < bodySize; written += buffer.Length)
            {
                var thisWriteSize = Math.Min(buffer.Length, bodySize - written);
                var span = builder.GetSpan(thisWriteSize);
                buffer.AsSpan(0, thisWriteSize).CopyTo(span);
                builder.Advance(thisWriteSize);
            }

            WriteHeader(builder);

            // Act

            builder.Reset();

            // Assert

            // This advances to increase length without actually overwriting the buffer (except for the header)
            builder.GetSpan(bodySize);
            builder.Advance(bodySize);
            WriteHeader(builder);

            Assert.True(builder.Length >= bodySize);

            var builderData = builder.GetBuffer().Span.Slice(OperationHeader.Length);
            var shouldMatch = new byte[builderData.Length];
            Assert.True(builderData.SequenceEqual(shouldMatch));
        }

        #endregion

        #region Ensure Capacity

        [Fact]
        private void EnsureCapacity_SameCapacity_NoChange()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(currentCapacity);

            // Assert

            Assert.Equal(currentCapacity, builder.Capacity);
        }

        [Fact]
        private void EnsureCapacity_LessThan_NoChange()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(currentCapacity - 1);

            // Assert

            Assert.Equal(currentCapacity, builder.Capacity);
        }

        [Fact]
        private void EnsureCapacity_Zero_NoChange()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(0);

            // Assert

            Assert.Equal(currentCapacity, builder.Capacity);
        }

        [Fact]
        private void EnsureCapacity_OneMore_Doubles()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(currentCapacity + 1);

            // Assert

            Assert.Equal(currentCapacity * 2, builder.Capacity);
        }

        [Fact]
        private void EnsureCapacity_Double_Doubles()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(currentCapacity * 2);

            // Assert

            Assert.Equal(currentCapacity * 2, builder.Capacity);
        }

        [Fact]
        private void EnsureCapacity_OneMoreThanDouble_Quadruples()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(currentCapacity * 2 + 1);

            // Assert

            Assert.Equal(currentCapacity * 4, builder.Capacity);
        }

        [Fact]
        private void EnsureCapacity_Quadruple_Quadruples()
        {
            // Arrange

            using var builder = new OperationBuilder();
            var currentCapacity = builder.Capacity;

            // Act

            builder.EnsureCapacity(currentCapacity * 4);

            // Assert

            Assert.Equal(currentCapacity * 4, builder.Capacity);
        }

        #endregion

        #region Helpers

        private static readonly OperationRequestHeader _header = new()
        {
            DataType = DataType.Json,
            Opaque = 55,
            OpCode = OpCode.Get,
            VBucketId = 5
        };

        private static void WriteHeader(OperationBuilder builder)
        {
            builder.WriteHeader(in _header);
        }

        private static void WriteKey(OperationBuilder builder, string key)
        {
            builder.AdvanceToSegment(OperationSegment.Key);

            var buffer = builder.GetSpan(OperationHeader.MaxKeyLength);
            var keyLength = ByteConverter.FromString(key, buffer);
            builder.Advance(keyLength);

            builder.AdvanceToSegment(OperationSegment.Body);
        }

        private static void AssertBuilder(OperationBuilder builder, string key, int bodySize,
            ReadOnlySpan<byte> startOfBody = default)
        {
            var expectedKeyLength = ByteConverter.GetStringByteCount(key);

            var expectedTotalLength = OperationHeader.Length + expectedKeyLength + bodySize;
            Assert.Equal(expectedTotalLength, builder.Length);

            var operation = builder.GetBuffer().Span;
            Assert.Equal(expectedTotalLength, operation.Length);

            var header = OperationHeader.Read(operation);
            Assert.Equal(expectedKeyLength + bodySize, header.BodyLength);
            Assert.Equal(expectedKeyLength, header.KeyLength);

            var readKey = ByteConverter.ToString(operation.Slice(OperationHeader.Length, header.KeyLength));
            Assert.Equal(key, readKey);

            if (!startOfBody.IsEmpty)
            {
                Assert.True(operation.Slice(OperationHeader.Length + header.KeyLength).StartsWith(startOfBody));
            }
        }

        private static void AssertMultiBuilder(OperationBuilder builder, string key, int numSpecs,
            int fragmentSize, ReadOnlySpan<byte> startOfEachSpec = default)
        {
            var expectedKeyLength = ByteConverter.GetStringByteCount(key);

            const int expectedPathLength = 1;
            int specHeaderSize = fragmentSize > 0 ? 8 : 4;
            int expectedSpecSizeWithHeader = specHeaderSize + expectedPathLength + fragmentSize;
            int expectedBodySize = numSpecs * expectedSpecSizeWithHeader;

            var expectedTotalLength = OperationHeader.Length + expectedKeyLength + expectedBodySize;
            Assert.Equal(expectedTotalLength, builder.Length);

            var operation = builder.GetBuffer().Span;
            Assert.Equal(expectedTotalLength, operation.Length);

            var header = OperationHeader.Read(operation);
            Assert.Equal(expectedKeyLength + expectedBodySize, header.BodyLength);
            Assert.Equal(expectedKeyLength, header.KeyLength);

            var readKey = ByteConverter.ToString(operation.Slice(OperationHeader.Length, header.KeyLength));
            Assert.Equal(key, readKey);

            var spec = operation.Slice(OperationHeader.Length + header.KeyLength);
            for (int i = 0; i < numSpecs; i++)
            {
                var pathLength = ByteConverter.ToInt16(spec.Slice(2));
                Assert.Equal(expectedPathLength, pathLength);

                var readPath = ByteConverter.ToString(spec.Slice(specHeaderSize, expectedPathLength));
                Assert.Equal($"{i}", readPath);

                var fragmentLength = 0;
                if (fragmentSize > 0)
                {
                    fragmentLength = ByteConverter.ToInt32(spec.Slice(4));
                    Assert.Equal(fragmentSize, fragmentLength);

                    if (!startOfEachSpec.IsEmpty)
                    {
                        Assert.True(spec.Slice(specHeaderSize + expectedPathLength, fragmentLength).StartsWith(startOfEachSpec));
                    }
                }

                spec = spec.Slice(specHeaderSize + pathLength + fragmentLength);
            }
        }

        #endregion
    }
}
