# Couchbase.NetClient.CombinationTests

## Purpose
A small project that tests the CouchbaseNetClient.nupkg - the ability to connect to different environments (OnPremise & Protostellar) with only a schema change and assuming the connection string is valid and points to two online clusters.

Most all testing should take place either in the on-premise Couchbase.UnitTests and/or the Couchbase.CombinationTests or for Protostellar/cloud the Couchbase.Stellar.CombinationTests and Couchbase.Stellar.UnitTests

**Note:**
Currently the dependencies are on amongst the projects themselves, eventually they should be done via the NuGet packages.
