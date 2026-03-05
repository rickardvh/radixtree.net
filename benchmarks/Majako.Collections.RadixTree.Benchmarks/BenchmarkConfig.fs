namespace Majako.Collections.RadixTree.Benchmarks

open BenchmarkDotNet.Columns
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Loggers

type RadixBenchmarkConfig() as this =
    inherit ManualConfig()

    do
        this.AddJob(Job.ShortRun.WithId("short")) |> ignore
        this.AddLogger(ConsoleLogger.Default) |> ignore
        this.AddColumnProvider(DefaultColumnProviders.Instance) |> ignore
        this.WithOptions(ConfigOptions.JoinSummary) |> ignore
