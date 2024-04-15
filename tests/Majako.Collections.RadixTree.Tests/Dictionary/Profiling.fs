namespace Majako.Collections.RadixTree.Tests.Dictionary

open Xunit
open Majako.Collections.RadixTree.Concurrent
open System
open Xunit.Abstractions
open Majako.Collections.RadixTree.Tests

type Profiling(output: ITestOutputHelper) =
    [<Literal>]
    let SKIP: string = "Profiling disabled" // set to null to enable profiling
    // let SKIP: string = null // set to null to enable profiling

    let profile = profile output

    [<Fact(Skip = SKIP)>]
    let ``Profile`` () =
        let sut = ConcurrentRadixTree<int>()

        let add _ =
            for _ = 1 to 10000 do
                sut.Add(Guid.NewGuid().ToString(), 0)

        profile <| fun () -> Array.Parallel.iter add [| 1..1000 |]

    [<Fact(Skip = SKIP)>]
    let ``Profile add/remove`` () =
        let sut = ConcurrentRadixTree<int>()

        let key (i, j) = $"{i}-{j}"

        let addRemove j =
            for i in 1..10000 do
                sut.Add(key (i, j), i)

            for i in 1..10000 do
                sut.Remove(key (i, j)) |> ignore

        profile <| fun () -> Array.Parallel.iter addRemove [| 1..1000 |]
