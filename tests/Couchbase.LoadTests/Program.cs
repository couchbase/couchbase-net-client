using System;
using BenchmarkDotNet.Running;

namespace Couchbase.LoadTests
{
    public class Program
    {
        static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new StandardConfig());
    }
}
