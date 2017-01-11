using System;

namespace Couchbase.Logging
{
    public interface ILog
    {
        bool IsDebugEnabled { get; }

        void Trace(string message, params object[] args);
        void Trace(Exception exception);
        void Trace(string message, Exception exception);

        void Debug(string format, params object[] args);
        void Debug(Exception exception);
        void Debug(string message, Exception exception);

        void Info(string format, params object[] args);
        void Info(Exception exception);
        void Info(string message, Exception exception);

        void Warn(string message, params object[] args);
        void Warn(Exception exception);
        void Warn(string message, Exception exception);

        void Error(string message, params object[] args);
        void Error(Exception exception);
        void Error(string message, Exception exception);
    }
}