using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Logging;

namespace Couchbase.UnitTests.Fakes
{
    public class FakeLog : ILog
    {
        private readonly string _logName;
        private readonly LogLevel _logLevel;
        private readonly bool _showlevel;
        private readonly bool _showDateTime;
        private readonly bool _showLogName;
        private readonly string _dateTimeFormat;

        public FakeLog(string logName, LogLevel logLevel, bool showlevel, bool showDateTime, bool showLogName, string dateTimeFormat)
        {
            _logName = logName;
            _logLevel = logLevel;
            _showlevel = showlevel;
            _showDateTime = showDateTime;
            _showLogName = showLogName;
            _dateTimeFormat = dateTimeFormat;
            LogStore = new StringBuilder();
        }

        public StringBuilder LogStore { get; set; }

        public bool IsDebugEnabled
        {
            get { return _logLevel == LogLevel.Debug; }
        }

        public void Trace(string format, params object[] args)
        {
            LogStore.AppendFormat(format, args);
        }

        public void Trace(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Trace(string message, Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Debug(string format, params object[] args)
        {
            LogStore.AppendFormat(format, args);
        }

        public void Debug(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Debug(string message, Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Info(string format, params object[] args)
        {
            LogStore.AppendFormat(format, args);
        }

        public void Info(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Info(string message, Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Warn(string format, params object[] args)
        {
            LogStore.AppendFormat(format, args);
        }

        public void Warn(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Warn(string message, Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Error(string format, params object[] args)
        {
            LogStore.AppendFormat(format, args);
        }

        public void Error(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Error(string message, Exception exception)
        {
            throw new NotImplementedException();
        }
    }
}
