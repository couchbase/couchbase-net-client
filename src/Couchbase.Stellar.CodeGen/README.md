# Couchbase.CodeGen

This project generates the gRPC stubs and proxies using the protos defined in the https://github.com/couchbase/protostellar repository. This project is intended to be used internally and is not required for general development as the stubs and proxies are committed to the https://github.com/couchbase/couchbase-net-client repo in the Couchbase.Stellar project.

## How to use it:
* Clone https://github.com/couchbase/couchbase-net-client
* Clone https://github.com/couchbase/protostellar in a directory adjacent to the couchbase-net-client repo.
* Build the entire couchbase-net-client solution and the stubs and proxies will be added to the obj/Debug/net6.0 directory in Couchbase.Stellar.CodeGen
* Copy the generated files into the Couchbase.Stellar project in the CodeGen directory. Note that these files are committed and only updated when the protos change.
* The directory structure should look like this:

```
- couchbase-net-client (cloned repo)
    - ..
    - src
        - ..
        - Couchbase.Stellar.CodeGen
        - ..
    - ..
- protostellar (cloned repo)
```
## Example
```
$ mkdir repos
$ cd repos
$ git clone git@github.com:couchbase/couchbase-net-client.git
$ git clone git@github.com:couchbase/protostellar.git
```

Now the two repos will be adjacent to each other and the MSBuild code in Couchbase.Stellar.CodeGen will be able to generate the stubs and proxies from the protos.

Note that the Couchbase.Stellar.CodeGen project build is disabled in the solution build configuration. The build must be enabled for the code generation to work as it will happen when the project is built.
Alternatively, run `dotnet build` in the Couchbase.Stellar.CodeGen folder.
