# Benchmarks

## Running Benchmarks

Benchmarks are run from the command line using `dotnet run`.

```sh
# Example command line, if current directory is the Couchbase.LoadTests folder
dotnet run -c Release -f netcoreapp2.1 -- --job short --runtimes netcoreapp2.0 netcoreapp2.1 net461 --filter *Read*
```

Documentation about command line options can be obtained via:

```sh
dotnet run -c Release -f netcoreapp2.1 -- -?
```

**Note:** `netcoreapp2.1` is included in the command line twice. The first, after `-f`, is the runtime used for launching the benchmarks. The second, after `--runtimes`, includes it in the list of runtimes to be benchmarked.

## Results

Benchmark results are printed to the console and output to several file formats. These files are in the `BenchmarkDotNet.Artifacts/results` directory after the run.

## Graphs

If you have R installed and rscript.exe in your PATH, graphs will also be generated in the results directory. R can be installed via `choco install microsoft-r-open` on Windows, though this **doesn't** add it to your PATH automatically.
