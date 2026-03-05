# Benchmarks

This folder contains BenchmarkDotNet benchmarks for `Majako.Collections.RadixTree`.

## Run all benchmarks

```powershell
dotnet run -c Release --project .\benchmarks\Majako.Collections.RadixTree.Benchmarks\Majako.Collections.RadixTree.Benchmarks.fsproj -- --exporters markdown csv
```

## Run a subset

```powershell
dotnet run -c Release --project .\benchmarks\Majako.Collections.RadixTree.Benchmarks\Majako.Collections.RadixTree.Benchmarks.fsproj -- --filter "*Concurrent*" --exporters markdown csv
```

Results are exported as Markdown and CSV in `BenchmarkDotNet.Artifacts`.
