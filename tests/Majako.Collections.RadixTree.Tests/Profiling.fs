module Majako.Collections.RadixTree.Tests.Profiling

open System.Diagnostics
open Xunit
open Majako.Collections.RadixTree
open System
open Xunit.Abstractions

type Scenarios(output: ITestOutputHelper) =
    let profile f =
        let sw = Stopwatch()
        let memory = GC.GetTotalMemory(true) >>> 20
        sw.Start()
        f ()
        sw.Stop()
        let delta = (GC.GetTotalMemory(true) >>> 20) - memory
        output.WriteLine(sprintf "Elapsed time: %.2f s" (float sw.ElapsedMilliseconds / 1000.0))
        output.WriteLine(sprintf "Memory usage: %.2f MB" (float delta))

    [<Fact>]
    let ``Profile``() =
        let sut = ConcurrentTrie<int>()

        let add _ =
            for _ = 1 to 1000 do
                sut.Add(Guid.NewGuid().ToString(), 0)

        profile <| fun () -> Array.Parallel.iter add [| 1..1000 |]
