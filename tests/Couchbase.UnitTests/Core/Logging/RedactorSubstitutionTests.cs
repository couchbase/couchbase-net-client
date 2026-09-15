using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core.Logging
{
    public class RedactorSubstitutionTests
    {
        private sealed class CustomRedactor : IRedactor
        {
            public object? UserData(object? message) => message;
            public object? MetaData(object? message) => message;
            public object? SystemData(object? message) => message;
        }

        private sealed class CapturingLogger : ILogger, ILoggerFactory
        {
            public readonly List<string> Warnings = new();

            public ILogger CreateLogger(string categoryName) => this;
            public void AddProvider(ILoggerProvider provider) { }
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Dispose() { }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }

        private static (ClusterOptions Options, List<string> Warnings) OptionsCapturingWarnings()
        {
            var logger = new CapturingLogger();
            return (new ClusterOptions { Logging = logger }, logger.Warnings);
        }

        [Fact]
        public void Replacing_The_Redactor_Warns()
        {
            var (options, warnings) = OptionsCapturingWarnings();
            options.AddClusterService<IRedactor, CustomRedactor>();

            options.BuildServiceProvider();

            Assert.Contains(warnings, w => w.Contains("IRedactor") && w.Contains("has been ignored"));
        }

        [Fact]
        public void Leaving_The_Redactor_Alone_Does_Not_Warn()
        {
            var (options, warnings) = OptionsCapturingWarnings();

            options.BuildServiceProvider();

            Assert.DoesNotContain(warnings, w => w.Contains("IRedactor"));
        }

        [Fact]
        public void A_Replaced_Redactor_Is_Not_Handed_Back_By_ClusterServices()
        {
            var options = new ClusterOptions();
            options.AddClusterService<IRedactor, CustomRedactor>();

            var provider = options.BuildServiceProvider();

            // The registration is discarded, so the documented way to fetch the cluster's redactor
            // always yields one that honours RedactionLevel.
            Assert.IsType<Redactor>(provider.GetService(typeof(IRedactor)));
            Assert.Same(provider.GetService(typeof(Redactor)), provider.GetService(typeof(IRedactor)));
        }

        [Fact]
        public void A_Replaced_Redactor_Is_Never_Consulted()
        {
            var options = new ClusterOptions();
            options.AddClusterService<IRedactor, CustomRedactor>();

            var provider = options.BuildServiceProvider();
            var factory = provider.GetService(typeof(IConnectionPoolScaleControllerFactory))!;
            var held = factory.GetType()
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Single(f => typeof(IRedactor).IsAssignableFrom(f.FieldType))
                .GetValue(factory);

            Assert.IsType<Redactor>(held);
        }
    }
}
