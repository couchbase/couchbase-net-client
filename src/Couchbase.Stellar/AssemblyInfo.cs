using System.Runtime.CompilerServices;

#if SIGNING
[assembly: InternalsVisibleTo("Couchbase.NetClient, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a3559ed637e93a" +
 "5ba4c059f97843d99a11d802431b6c6eb9f2e07ff3176747006709ec34b44ce93632fa3a832b16" +
 "243a97bf32abb9f527440683fc61df9757b1c17d5a9236bb55b0fb8b2fdedffd6baa75a2ea2ff9" +
 "30f7247bfe639ecb6f42a9706cec0af30ea3c7b4ec76e4cabb983fc5bb889260090fda4a7646a3" +
 "92ae04ca")]
#else
[assembly: InternalsVisibleTo("Couchbase.NetClient")]
#endif
