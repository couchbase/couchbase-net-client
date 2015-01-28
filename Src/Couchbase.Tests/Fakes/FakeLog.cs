using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Simple;

namespace Couchbase.Tests.Fakes
{
    public class FakeLog : AbstractSimpleLogger
    {
        public FakeLog(string logName, LogLevel logLevel, bool showlevel, bool showDateTime, bool showLogName, string dateTimeFormat) 
            : base(logName, logLevel, showlevel, showDateTime, showLogName, dateTimeFormat)
        {
            LogStore = new StringBuilder();
        }

        protected override void WriteInternal(LogLevel level, object message, Exception exception)
        {
            FormatOutput(LogStore, level, message, exception);
        }

        public StringBuilder LogStore { get; set; }
    }
}
