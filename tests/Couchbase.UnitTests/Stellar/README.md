Any files in this directory that target Couchbase.Stellar namespace should be annotated with:
#if NETCOREAPP3_1_OR_GREATER
...
#endif

The reason is that anything stellar will not compile on earlier runtimes such as .NET 4.8.
