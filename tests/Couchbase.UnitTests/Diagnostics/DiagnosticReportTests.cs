using System;
using System.Collections.Generic;
using System.Text.Json;
using Couchbase.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Diagnostics
{
    public class DiagnosticReportTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DiagnosticReportTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ToString_Success()
        {
            // Arrange

            var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>
            {
                ["kv"] = new List<EndpointDiagnostics>
                {
                    new()
                    {
                        Id = "xyz",
                        Local = "local"
                    }
                }
            };

            var report = new DiagnosticsReport("reportId", endpoints);

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

            var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>
            {
                ["kv"] = new List<EndpointDiagnostics>
                {
                    new()
                    {
                        Id = "xyz",
                        Local = "local"
                    }
                }
            };

            var report = new DiagnosticsReport("reportId", endpoints);

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

            var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>
            {
                ["kv"] = new List<EndpointDiagnostics>
                {
                    new()
                    {
                        Id = "xyz",
                        Local = "local"
                    }
                }
            };

            var report = new DiagnosticsReport("reportId", endpoints);

            // Act

            var result = JsonSerializer.Serialize<IDiagnosticsReport>(report);

            // Assert

            Assert.NotNull(result);
            _testOutputHelper.WriteLine(result);
        }
    }
}
