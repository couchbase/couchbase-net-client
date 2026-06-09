using Couchbase.Grpc.Protocol.Transactions;
using Couchbase.Query;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.FitPerformer.Utils
{
    internal class ResultValidation
    {
        internal static async Task ValidateQueryResult(CommandQuery queryRequest, IQueryResult<object> results)
        {
            List<object> rows = null;
            if (queryRequest.CheckRowCount)
            {
                rows = await CheckRowCount(queryRequest, results);
            }

            if (queryRequest.CheckMutations)
            {
                var actual = results.MetaData?.Metrics?.MutationCount;
                if (actual != queryRequest.ExpectedMutations)
                {
                    throw new TestFailureException("Invalid mutation count", new InvalidOperationException($"Expected {queryRequest.ExpectedMutations} mutations but only got {actual}"));
                }
            }

            if (queryRequest.CheckRowContent)
            {
                rows ??= await CheckRowCount(queryRequest, results);

                for (int i = 0; i < rows.Count; i++)
                {
                    var expected = JObject.Parse(queryRequest.ExpectedRows[i]);
                    var row = JObject.FromObject(rows[i]);
                    var expectedProps = expected.Properties().ToList();
                    var rowProps = row.Properties().ToList();
                    if (expectedProps.Count != rowProps.Count)
                    {
                        throw new TestFailureException($"Invalid field count for row {i}", new InvalidOperationException($"Expected {expectedProps.Count} fields in row {i} but only got {rowProps.Count}"));
                    }

                    foreach (var prop in expectedProps)
                    {
                        if (!row.ContainsKey(prop.Name))
                        {
                            throw new TestFailureException($"Missing field for row {i}", new InvalidOperationException($"Expected field {prop.Name} in row {i} but not found"));
                        }

                        if (row[prop.Name] != prop)
                        {
                            throw new TestFailureException($"Unexpected value for row {i}", new InvalidOperationException($"Expected {prop.Name} = {prop} in row {i} but got {row[prop.Name]}"));
                        }
                    }
                }
            }
        }

        private static async Task<List<object>> CheckRowCount(CommandQuery queryRequest, IQueryResult<object> results)
        {
            var rows = await results.Rows.ToListAsync();
            if (rows.Count != queryRequest.ExpectedRowCount)
            {
                throw new TestFailureException("Invalid row count", new InvalidOperationException($"Expected {queryRequest.ExpectedRowCount} rows but only got {rows.Count}"));
            }

            return rows;
        }
    }
}
