using System;
using System.Buffers;
using Couchbase.Test.Common.Utils;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class Utf8HelpersTests
    {
        #region TrimBomIfPresent_Span

        [Fact]
        public void TrimBomIfPresent_Span_Empty_ReturnsEmpty()
        {
            // Arrange

            ReadOnlySpan<byte> input = default;

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.Equal(0, result.Length);
        }

        [Fact]
        public void TrimBomIfPresent_Span_ShorterThanBom_ReturnsSpan()
        {
            // Arrange

            ReadOnlySpan<byte> input = [0x01];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input));
        }

        [Fact]
        public void TrimBomIfPresent_Span_LongerThanBom_ReturnsSpan()
        {
            // Arrange

            ReadOnlySpan<byte> input = [0x01, 0x02, 0x03, 0x04];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input));
        }

        [Fact]
        public void TrimBomIfPresent_Span_OnlyBom_ReturnsEmpty()
        {
            // Arrange

            ReadOnlySpan<byte> input = Utf8Helpers.Utf8Bom;

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.Equal(0, result.Length);
        }

        [Fact]
        public void TrimBomIfPresent_Span_Bom_TrimsBom()
        {
            // Arrange

            ReadOnlySpan<byte> input = [.. Utf8Helpers.Utf8Bom, 0x01, 0x02, 0x03, 0x04];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input.Slice(Utf8Helpers.Utf8Bom.Length)));
        }

        #endregion

        #region TrimBomIfPresent_Memory

        [Fact]
        public void TrimBomIfPresent_Memory_Empty_ReturnsEmpty()
        {
            // Arrange

            ReadOnlyMemory<byte> input = default;

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.Equal(0, result.Length);
        }

        [Fact]
        public void TrimBomIfPresent_Memory_ShorterThanBom_ReturnsSpan()
        {
            // Arrange

            ReadOnlyMemory<byte> input = (byte[])[0x01];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.Span.SequenceEqual(input.Span));
        }

        [Fact]
        public void TrimBomIfPresent_Memory_LongerThanBom_ReturnsSpan()
        {
            // Arrange

            ReadOnlyMemory<byte> input = (byte[])[0x01, 0x02, 0x03, 0x04];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.Span.SequenceEqual(input.Span));
        }

        [Fact]
        public void TrimBomIfPresent_Memory_OnlyBom_ReturnsEmpty()
        {
            // Arrange

            ReadOnlyMemory<byte> input = (byte[])[.. Utf8Helpers.Utf8Bom];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.Equal(0, result.Length);
        }

        [Fact]
        public void TrimBomIfPresent_Memory_Bom_TrimsBom()
        {
            // Arrange

            ReadOnlyMemory<byte> input = (byte[])[.. Utf8Helpers.Utf8Bom, 0x01, 0x02, 0x03, 0x04];

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.Span.SequenceEqual(input.Span.Slice(Utf8Helpers.Utf8Bom.Length)));
        }

        #endregion

        #region TrimBomIfPresent_Sequence

        [Fact]
        public void TrimBomIfPresent_Sequence_Empty_ReturnsEmpty()
        {
            // Arrange

            ReadOnlySequence<byte> input = default;

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.Equal(0, result.Length);
        }

        [Fact]
        public void TrimBomIfPresent_Sequence_ShorterThanBom_ReturnsSpan()
        {
            // Arrange

            ReadOnlySequence<byte> input = new([0x01]);

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input));
        }

        [Fact]
        public void TrimBomIfPresent_Sequence_LongerThanBom_ReturnsSpan()
        {
            // Arrange

            ReadOnlySequence<byte> input = new([0x01, 0x02, 0x03, 0x04]);

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input));
        }

        [Fact]
        public void TrimBomIfPresent_Sequence_OnlyBom_ReturnsEmpty()
        {
            // Arrange

            ReadOnlySequence<byte> input = new([.. Utf8Helpers.Utf8Bom]);

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.Equal(0, result.Length);
        }

        [Fact]
        public void TrimBomIfPresent_Sequence_Bom_TrimsBom()
        {
            // Arrange

            ReadOnlySequence<byte> input = new([.. Utf8Helpers.Utf8Bom, 0x01, 0x02, 0x03, 0x04]);

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input.Slice(Utf8Helpers.Utf8Bom.Length)));
        }

        [Fact]
        public void TrimBomIfPresent_Sequence_SplitBom_TrimsBom()
        {
            // Arrange

            ReadOnlySequence<byte> input = SequenceHelpers.CreateSequenceWithMaxSegmentSize(
                (byte[])[.. Utf8Helpers.Utf8Bom, 0x01, 0x02, 0x03, 0x04], 2);

            // Act
            var result = Utf8Helpers.TrimBomIfPresent(input);

            // Assert

            Assert.True(result.SequenceEqual(input.Slice(Utf8Helpers.Utf8Bom.Length)));
        }

        #endregion
    }
}
