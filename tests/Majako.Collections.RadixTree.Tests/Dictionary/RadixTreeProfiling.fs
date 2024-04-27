namespace Majako.Collections.RadixTree.Tests.Dictionary

open Xunit
open Majako.Collections.RadixTree
open System
open Xunit.Abstractions
open Majako.Collections.RadixTree.Tests

type RadixTreeProfiling(output: ITestOutputHelper) =
    [<Literal>]
    let SKIP: string = "Profiling disabled" // set to null to enable profiling

    let profile = profile output

    [<Fact(Skip = SKIP)>]
    let ``Profile`` () =
        let sut = RadixTree<int>()

        let add _ =
            for _ = 1 to 10000 do
                sut.Add(Guid.NewGuid().ToString(), 0)

        profile <| fun () -> Array.iter add [| 1..1000 |]

    [<Fact(Skip = SKIP)>]
    let ``Profile add/remove`` () =
        let sut = RadixTree<int>()

        let key (i, j) = $"{i}-{j}"

        let addRemove j =
            for i in 1..10000 do
                sut.Add(key (i, j), i)

            for i in 1..10000 do
                sut.Remove(key (i, j)) |> ignore

        profile <| fun () -> Array.iter addRemove [| 1..1000 |]
