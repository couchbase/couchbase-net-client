namespace Couchbase.Query
{
    public class QueryWarning
    {
        public string Message { get; set; }

        public int Code { get; set; }
    }

    internal class WarningData
    {
        public string msg { get; set; }
        public int code { get; set; }

        internal QueryWarning ToWarning()
        {
            return new QueryWarning
            {
                Message = msg,
                Code = code,
            };
        }
    }
}