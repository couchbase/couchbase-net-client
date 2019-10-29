namespace Couchbase.Query
{
    public static class QueryExceptionExtensions
    {
        public static bool IsRetryable(this QueryException exception)
        {
            foreach (var error in exception.Errors)
            {
                switch (error.Code)
                {
                    case 4040:
                    case 4050:
                    case 4070:
                        return true;
                    case 5000:
                        return (error.Message != null
                                && error.Message.Contains(QueryClient.Error5000MsgQueryPortIndexNotFound));
                    default:
                       continue;
                }
            }

            return false;
        }
    }
}
