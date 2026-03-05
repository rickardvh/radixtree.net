// For more information see https://aka.ms/fsharp-console-apps
module Majako.Collections.RadixTree.Benchmarks.Program

open BenchmarkDotNet.Running

type Program = class end

[<EntryPoint>]
let main argv =
    BenchmarkSwitcher.FromAssembly(typeof<Program>.Assembly).Run(argv, RadixBenchmarkConfig()) |> ignore
    0
