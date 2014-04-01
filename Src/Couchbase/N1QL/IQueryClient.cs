using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Views;

namespace Couchbase.N1QL
{
    internal interface IQueryClient
    {
        IQueryResult<T> Query<T>(Uri server, string query);

        IDataMapper DataMapper { get; set; }

        HttpClient HttpClient { get; set; }
    }
}
