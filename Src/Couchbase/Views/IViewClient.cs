using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    internal interface IViewClient
    {
        Task<IViewResult<T>> ExecuteAsync<T>(IViewQuery query);

        IViewResult<T> Execute<T>(IViewQuery query);

        IDataMapper Mapper { get; set; }
            
        HttpClient HttpClient { get; set; }
    }
}
