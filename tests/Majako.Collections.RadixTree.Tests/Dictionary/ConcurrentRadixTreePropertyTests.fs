namespace Majako.Collections.RadixTree.Tests.Dictionary

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree.Concurrent

type ConcurrentRadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentRadixTree<int>(items))

    [<Property>]
    member _.``Parallel insertions should keep all inserted keys reachable``(keys: list<NonEmptyString>) =
        let distinctKeys = keys |> List.map (fun k -> k.Get) |> List.distinct
        let sut = ConcurrentRadixTree<int>()

        [| 1 .. 8 |]
        |> Array.Parallel.iter (fun worker ->
            for key in distinctKeys do
                sut.[key] <- worker)

        distinctKeys
        |> List.forall (fun key ->
            let found, _ = sut.TryGetValue key
            found)
