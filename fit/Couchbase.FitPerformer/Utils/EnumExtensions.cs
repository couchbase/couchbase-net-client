using System;
using Microsoft.Extensions.Logging;
#nullable enable

namespace Couchbase.FitPerformer.Utils
{
    public static class EnumExtensions
    {
        public static LogLevel? Parse(this string? value)
        {
            return (value?.ToLower()) switch
            {
                "info" => LogLevel.Information,
                "debug" => LogLevel.Debug,
                "warn" => LogLevel.Warning,
                "trace" => LogLevel.Trace,
                "error" => LogLevel.Error,
                "off" => LogLevel.None,
                _ => LogLevel.Information,
            };
        }
    }
}