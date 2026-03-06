namespace Majako.Collections.RadixTree.Tests.Dictionary

open Xunit
open Swensen.Unquote
open Majako.Collections.RadixTree.Concurrent

type ConcurrentRadixTreeTests() =
    inherit PrefixTreeTestBase(fun () -> ConcurrentRadixTree<int>())

    [<Fact>]
    let ``Does not break during parallel add remove`` () =
        let sut = ConcurrentRadixTree<int>()

        let removeEventually (key: string) =
            let mutable cleared = false
            let mutable attempts = 0

            while not cleared && attempts < 8 do
                if sut.Remove key then
                    cleared <- true
                else
                    let found, _ = sut.TryGetValue key
                    if not found then
                        cleared <- true
                    else
                        attempts <- attempts + 1

            if not cleared then
                let found, _ = sut.TryGetValue key
                cleared <- not found

            cleared

        let addRemove j =
            for i in 1..1000 do
                let key = $"{i}-{j}"
                sut.Add(key, i)
                let found, value = sut.TryGetValue key
                test <@ found @>
                value =! i
                test <@ removeEventually key @>
                let found, _ = sut.TryGetValue key
                test <@ not found @>

        Array.Parallel.iter addRemove [| 1..100 |]

        test <@ Seq.isEmpty sut.Keys @>

    [<Fact>]
    let ``Does not break during parallel add prune`` () =
        let sut = ConcurrentRadixTree<int>()

        let addPrune j =
            let n = 1000

            for i in 1..n do
                sut.Add($"{j}-{i}", i)

            let subtree = sut.Prune $"{j}-"
            Seq.length subtree.Keys =! n

        Array.Parallel.iter addPrune [| 1..100 |]

        test <@ Seq.isEmpty sut.Keys @>
