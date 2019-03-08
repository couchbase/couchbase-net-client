namespace Couchbase.Services.Query
{
    public class Warning
    {
        public string Message { get; set; }

        public int Code { get; set; }
    }

    internal class WarningData
    {
        public string msg { get; set; }
        public int code { get; set; }

        internal Warning ToWarning()
        {
            return new Warning
            {
                Message = msg,
                Code = code,
            };
        }
    }
}