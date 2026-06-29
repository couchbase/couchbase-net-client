Building the API documentation

Metadata is generated from the compiled assemblies, because the
SDK uses newer C# syntax than DocFX's bundled Roslyn can parse.

1. Build the SDK first so the DLLs and XML doc files exist:
     dotnet build ../../src/Couchbase/Couchbase.csproj -c Release -f net10.0
     dotnet build ../../src/Couchbase.Extensions.DependencyInjection/Couchbase.Extensions.DependencyInjection.csproj -c Release -f net10.0
2. Replace __CB_SDK_VERSION__ in index.md with the SDK version.
3. Generate API metadata and the site:
     docfx metadata docfx.json
     docfx build docfx.json
4. The API docs are in the _site folder.

The apidocs GitHub Actions workflow automates these steps.
