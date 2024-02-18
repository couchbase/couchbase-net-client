using Couchbase.Core.Diagnostics.Metrics;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics
{
    public class HistogramCollectorTests
    {
        #region CollectMeasurements

        [Fact]
        public void CollectMeasurements_NoData_AllZeroes()
        {
            // Arrange

            var collector = new HistogramCollector();

            // Act

            var result = collector.CollectMeasurements();

            // Assert

            Assert.Equal(0, result.TotalCount);
            Assert.Equal(0, result.Percentiles._750);
            Assert.Equal(0, result.Percentiles._950);
            Assert.Equal(0, result.Percentiles._980);
            Assert.Equal(0, result.Percentiles._999);
            Assert.Equal(0, result.Percentiles._10000);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(1024)]
        [InlineData(-1)]
        [InlineData(-2)]
        [InlineData(-1024)]
        [InlineData(1.0009765625)] // This fits in the 10-bit truncated mantissa
        [InlineData(-1.0009765625)] // This fits in the 10-bit truncated mantissa
        public void CollectMeasurements_SingleLowPrecisionValueRepeated_IsAllPercentiles(double value)
        {
            // Arrange

            var collector = new HistogramCollector();

            // Act

            for (var i = 0; i < 4000; i++)
            {
                collector.AddMeasurement(value);
            }

            var result = collector.CollectMeasurements();

            // Assert

            Assert.Equal(4000, result.TotalCount);
            Assert.Equal(value, result.Percentiles._750);
            Assert.Equal(value, result.Percentiles._950);
            Assert.Equal(value, result.Percentiles._980);
            Assert.Equal(value, result.Percentiles._999);
            Assert.Equal(value, result.Percentiles._10000);
        }

        [Fact]
        public void CollectMeasurements_ManySingleValues_CorrectPercentiles()
        {
            // Arrange

            var collector = new HistogramCollector();

            // Act

            for (var i = 1; i <= 100; i++)
            {
                collector.AddMeasurement(i);
            }

            var result = collector.CollectMeasurements();

            // Assert

            Assert.Equal(100, result.TotalCount);
            Assert.Equal(75, result.Percentiles._750);
            Assert.Equal(95, result.Percentiles._950);
            Assert.Equal(98, result.Percentiles._980);
            Assert.Equal(99, result.Percentiles._999);
            Assert.Equal(100, result.Percentiles._10000);
        }

        [Fact]
        public void CollectMeasurements_ManySingleValuesMixedSigns_CorrectPercentiles()
        {
            // Arrange

            var collector = new HistogramCollector();

            // Act

            for (var i = 1; i <= 100; i++)
            {
                collector.AddMeasurement(i - 50);
            }

            var result = collector.CollectMeasurements();

            // Assert

            Assert.Equal(100, result.TotalCount);
            Assert.Equal(25, result.Percentiles._750);
            Assert.Equal(45, result.Percentiles._950);
            Assert.Equal(48, result.Percentiles._980);
            Assert.Equal(49, result.Percentiles._999);
            Assert.Equal(50, result.Percentiles._10000);
        }

        [Fact]
        public void CollectMeasurements_ManyMultipleValues_CorrectPercentiles()
        {
            // Arrange

            var collector = new HistogramCollector();

            // Act

            for (var i = 1; i <= 100; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    collector.AddMeasurement(i);
                }
            }

            var result = collector.CollectMeasurements();

            // Assert

            Assert.Equal(1000, result.TotalCount);
            Assert.Equal(75, result.Percentiles._750);
            Assert.Equal(95, result.Percentiles._950);
            Assert.Equal(98, result.Percentiles._980);
            Assert.Equal(100, result.Percentiles._999); // 99.9th percentile is 100
            Assert.Equal(100, result.Percentiles._10000);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CollectMeasurements_ManySingleValuesSameExponent_CorrectPercentiles(bool negative)
        {
            // Arrange

            var collector = new HistogramCollector();

            var baseExponent = 1.0 * (negative ? -1 : 1);
            const double step = 0.0009765625;

            // Act

            for (var i = 1; i <= 100; i++)
            {
                collector.AddMeasurement(baseExponent + i * step);
            }

            var result = collector.CollectMeasurements();

            // Assert

            Assert.Equal(100, result.TotalCount);
            Assert.Equal(baseExponent + 75 * step, result.Percentiles._750);
            Assert.Equal(baseExponent + 95 * step, result.Percentiles._950);
            Assert.Equal(baseExponent + 98 * step, result.Percentiles._980);
            Assert.Equal(baseExponent + 99 * step, result.Percentiles._999);
            Assert.Equal(baseExponent + 100 * step, result.Percentiles._10000);
        }

        #endregion
    }
}
