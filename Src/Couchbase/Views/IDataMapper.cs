using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    internal interface IDataMapper
    {
        T Map<T>(Stream stream);

        List<T> MapAll<T>(Stream stream);
    }
}
