module Majako.Collections.RadixTree.Tests.Profiling

open System.Diagnostics
open Xunit
open Majako.Collections.RadixTree.Concurrent
open System
open Xunit.Abstractions

[<Literal>]
let SKIP: string = "Profiling disabled" // set to null to enable profiling
// let SKIP: string = null // set to null to enable profiling

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

    [<Fact(Skip = SKIP)>]
    let ``Profile`` () =
        let sut = ConcurrentTrie<int>()

        let add _ =
            for _ = 1 to 10000 do
                sut.Add(Guid.NewGuid().ToString(), 0)

        profile <| fun () -> Array.Parallel.iter add [| 1..1000 |]

    [<Fact(Skip = SKIP)>]
    let ``Profile add/remove`` () =
        let sut = ConcurrentTrie<int>()

        let key(i, j) = $"{i}-{j}"

        let addRemove j =
            for i in 1..10000 do
                sut.Add(key(i, j), i)
            for i in 1..10000 do
                sut.Remove(key(i, j)) |> ignore

        profile <| fun () -> Array.Parallel.iter addRemove [| 1..1000 |]
