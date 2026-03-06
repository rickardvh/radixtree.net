namespace Majako.Collections.RadixTree.Tests.Set

open Xunit
open Swensen.Unquote
open Majako.Collections.RadixTree.Concurrent

type ConcurrentRadixTreeTests() =
    inherit
        PrefixTreeTestBase(fun items ->
            if items.IsSome then
                ConcurrentRadixTree items.Value
            else
                ConcurrentRadixTree())

    [<Fact>]
    let ``Does not block while enumerating`` () =
        let sut = ConcurrentRadixTree()
        sut.Add "a" |> ignore
        sut.Add "ab" |> ignore

        for item in sut do
            sut.Remove item |> ignore

    [<Fact>]
    let ``Does not break during parallel add remove`` () =
        let sut = ConcurrentRadixTree()

        let removeEventually (key: string) =
            let mutable cleared = false
            let mutable attempts = 0

            while not cleared && attempts < 8 do
                if sut.Remove key then
                    cleared <- true
                elif not (sut.Contains key) then
                    cleared <- true
                else
                    attempts <- attempts + 1

            if not cleared then
                cleared <- not (sut.Contains key)

            cleared

        let addRemove j =
            for i in 1..1000 do
                let key = $"{i}-{j}"
                sut.Add key |> ignore
                test <@ sut.Contains key @>
                test <@ removeEventually key @>
                test <@ not <| sut.Contains key @>

        Array.Parallel.iter addRemove [| 1..100 |]

        test <@ Seq.isEmpty sut @>

    [<Fact>]
    let ``Does not break during parallel add prune`` () =
        let sut = ConcurrentRadixTree()

        let addPrune j =
            let n = 1000

            for i in 1..n do
                sut.Add $"{j}-{i}" |> ignore

            let subtree = sut.Prune $"{j}-"
            Seq.length subtree =! n

        Array.Parallel.iter addPrune [| 1..100 |]

        test <@ Seq.isEmpty sut @>
