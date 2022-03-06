using System;
using System.Collections.Generic;
using System.Text.Json;
using Couchbase.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Diagnostics
{
    public class EndpointDiagnosticsTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EndpointDiagnosticsTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ToString_Success()
        {
            // Arrange

            var report = new EndpointDiagnostics
            {
                Id = "xyz",
                Local = "local",
                State = ServiceState.Connected
            };

            // Act

            var result = report.ToString();

            // Assert

            Assert.NotNull(result);
            _testOutputHelper.WriteLine(result);
        }

        [Fact]
        public void NewtonsoftSerialization_Success()
        {
            // Arrange

            var report = new EndpointDiagnostics
            {
                Id = "xyz",
                Local = "local",
                State = ServiceState.Connected
            };

            // Act

            var result = Newtonsoft.Json.JsonConvert.SerializeObject(report);

            // Assert

            Assert.NotNull(result);
            _testOutputHelper.WriteLine(result);
        }

        [Fact]
        public void SystemTextJsonSerialization_Success()
        {
            // Arrange

            var report = new EndpointDiagnostics
            {
                Id = "xyz",
                Local = "local",
                State = ServiceState.Connected
            };

            // Act

            var result = JsonSerializer.Serialize<IEndpointDiagnostics>(report);

            // Assert

            Assert.NotNull(result);
            _testOutputHelper.WriteLine(result);
        }
    }
}
