using System.Linq;
using Couchbase.Analytics;

namespace Couchbase.Core.Exceptions.Analytics
{
    internal static class AnalyticResultsExtensions
    {
        public static bool CompilationFailure<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(error => error.Code > 24000 && error.Code < 25000);
        }

        public static bool JobQueueFull<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(error => error.Code == 23007);
        }

        public static bool DataSetNotFound<T>(this AnalyticsResult<T> result)
        {
            //24044, 24045, 24025
            return result.Errors.Any(error => error.Code == 24044 || error.Code == 24045 || error.Code == 24025);
        }

        public static bool DataverseNotFound<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(error => error.Code == 24034);
        }

        public static bool DatasetExists<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(error => error.Code == 24040);
        }

        public static bool DataverseExists<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(error => error.Code == 24039);
        }

        public static bool LinkNotFound<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(error => error.Code == 24006);
        }

        public static bool InternalServerFailure<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(x => x.Code >= 25000 && x.Code < 26000);
        }

        public static bool AuthenticationFailure<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(x => x.Code >= 20000 && x.Code < 21000);
        }

        public static bool TemporaryFailure<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(x => x.Code >= 23000 && x.Code < 23003);
        }

        public static bool ParsingFailure<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(x => x.Code >= 24000);
        }

        public static bool IndexNotFound<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(x => x.Code >= 24007);
        }

        public static bool IndexExists<T>(this AnalyticsResult<T> result)
        {
            return result.Errors.Any(x => x.Code >= 24008);
        }
    }
}
