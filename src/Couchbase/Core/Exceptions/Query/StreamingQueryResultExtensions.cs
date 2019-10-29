using System;
using System.Collections.Generic;
using Couchbase.Query;

namespace Couchbase.Core.Exceptions.Query
{
    public static class StreamingQueryResultExtensions
    {
        private static readonly List<int> PreparedErrorCodes = new List<int>
        {
            4040,
            4050,
            4060,
            4070,
            4080,
            4090
        };

        public static Exception ThrowExceptionOnError<T>(this StreamingQueryResult<T> result, QueryErrorContext context)
        {
            foreach (var error in result.Errors)
            {
                if (error.Code == 3000) throw new ParsingFailureException(context);

                if (PreparedErrorCodes.Contains(error.Code)) throw new PreparedStatementException(context);

                if (error.Code == 4300 && error.Message.Contains("index") &&
                    error.Message.Contains("already exists"))
                    throw new IndexExistsException(context);

                if (error.Code >= 4000 && error.Code < 5000) throw new PlanningFailureException(context);

                if (error.Code == 12004 || error.Code == 12016 ||
                    error.Code == 5000 && error.Message.Contains("index") && error.Message.Contains("not found"))
                    throw new IndexNotFoundException(context);

                if (error.Code == 5000 && error.Message.Contains("index") && error.Message.Contains("already exists"))
                    throw new IndexExistsException(context);

                if (error.Code >= 5000 && error.Code < 6000) throw new InternalServerFailureException();

                if (error.Code == 12009) throw new CasMismatchException(context);

                if (error.Code >= 10000 && error.Code < 11000)
                    throw new AuthenticationFailureException("Could not authenticate query.")
                    {
                        Context = context
                    };

                if (error.Code >= 12000 && error.Code < 13000 || error.Code >= 14000 && error.Code < 15000)
                    throw new IndexFailureException(context);
            }

            throw new CouchbaseException(context);
        }

        public static bool InternalFailure<T>(this StreamingQueryResult<T> result, out bool isRetriable)
        {
            isRetriable = false;
            foreach (var error in result.Errors)
            {
                if (error.Code >= 5000 && error.Code < 6000)
                {
                    isRetriable = (error.Message != null
                     && error.Message.Contains(QueryClient.Error5000MsgQueryPortIndexNotFound));
                    return true;
                }
            }

            return false;
        }
    }
}
