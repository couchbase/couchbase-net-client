namespace Couchbase.Analytics
{
    public class AnalyticsWarning
    {
        public string Message { get; set; }

        public int Code { get; set; }
    }

    internal class WarningData
    {
        public string msg { get; set; }
        public int code { get; set; }

        internal AnalyticsWarning ToWarning()
        {
            return new AnalyticsWarning
            {
                Message = msg,
                Code = code,
            };
        }
    }
}