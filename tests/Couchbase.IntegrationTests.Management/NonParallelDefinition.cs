using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    [CollectionDefinition("NonParallel", DisableParallelization = true)]

    internal class NonParallelDefinition
    {
    }
}
