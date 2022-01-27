using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace Couchbase.Transactions.Tests.IntegrationTests
{
    public class TestOutputLogger : ILogger
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly LogLevel _logLevel;

        public TestOutputLogger(ITestOutputHelper outputHelper, LogLevel logLevel)
        {
            _outputHelper = outputHelper;
            _logLevel = logLevel;
        }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                try
                {
                    _outputHelper.WriteLine($"{logLevel}: [{eventId}] {formatter(state, exception)}");
                }
                catch
                {
                    // multi-threaded code can cause the test output helper to throw if logged to after the test is finished.
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel) => _logLevel <= logLevel;

        public IDisposable BeginScope<TState>(TState state) => new Mock<IDisposable>().Object;
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