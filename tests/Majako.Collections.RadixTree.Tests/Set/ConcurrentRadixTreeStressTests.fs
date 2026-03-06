namespace Majako.Collections.RadixTree.Tests.Set

open Majako.Collections.RadixTree.Concurrent
open Majako.Collections.RadixTree.Tests
open Swensen.Unquote
open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading.Tasks
open Xunit

type ConcurrentRadixTreeStressTests() =
    let buildItems seed prefix count =
        let rng = Random(seed)

        [ for i in 0 .. count - 1 ->
              let shard = i % 16
              let salt = rng.Next(0, 4096)
              $"{prefix}{shard}-{salt:D4}-{i:D4}" ]

    [<Fact>]
    [<Trait("Category", "Stress")>]
    member _.``Deterministic mixed parallel workloads should match reference hash set``() =
        let completed =
            completesWithin (TimeSpan.FromSeconds 45.0) (fun () ->
                let runScenario seed =
                    let items = buildItems seed $"k-{seed}-" 256
                    let sut = ConcurrentRadixTree()
                    let expected = HashSet<string>()

                    let applyOps (target: HashSet<string>) i item =
                        target.Add(item) |> ignore

                        if (i + seed) % 2 = 0 then
                            target.Remove(item) |> ignore
                        else
                            target.Add(item) |> ignore

                        if (i + seed) % 3 = 0 then
                            target.Add(item) |> ignore

                        if (i + seed) % 5 = 0 then
                            target.Remove(item) |> ignore

                    items |> List.iteri (fun i item -> applyOps expected i item)

                    let options = ParallelOptions(MaxDegreeOfParallelism = 8)

                    Parallel.For(0, items.Length, options, fun i ->
                        let item = items[i]
                        sut.Add(item) |> ignore

                        if (i + seed) % 2 = 0 then
                            sut.Remove(item) |> ignore
                        else
                            sut.Add(item) |> ignore

                        if (i + seed) % 3 = 0 then
                            sut.Add(item) |> ignore

                        if (i + seed) % 5 = 0 then
                            sut.Remove(item) |> ignore)
                    |> ignore

                    let actualSet = sut |> Set.ofSeq
                    let expectedSet = expected |> Set.ofSeq
                    test <@ actualSet = expectedSet @>

                [ 17; 43; 97 ] |> List.iter runScenario
                true)

        test <@ completed @>

    [<Fact>]
    [<Trait("Category", "Stress")>]
    member _.``Deterministic prune-add-remove races should preserve keep-prefix items``() =
        let completed =
            completesWithin (TimeSpan.FromSeconds 45.0) (fun () ->
                let runScenario seed =
                    let prunePrefix = $"p-{seed}-"
                    let keepPrefix = $"k-{seed}-"
                    let pruneItems = buildItems (seed + 11) prunePrefix 96
                    let keepItems = buildItems (seed + 23) keepPrefix 96
                    let sut = ConcurrentRadixTree()
                    let prunedItems = ConcurrentBag<string>()

                    pruneItems |> List.iter (fun item -> sut.Add(item) |> ignore)
                    keepItems |> List.iter (fun item -> sut.Add(item) |> ignore)

                    let options = ParallelOptions(MaxDegreeOfParallelism = 6)

                    Parallel.For(0, 6, options, fun worker ->
                        if worker = 0 then
                            for round in 0 .. 199 do
                                let subtree = sut.Prune(prunePrefix)
                                for item in subtree do
                                    prunedItems.Add(item)

                                if round % 3 = 0 then
                                    for i in 0 .. 15 do
                                        let item = pruneItems[(round + i) % pruneItems.Length]
                                        sut.Add(item) |> ignore
                        elif worker = 1 then
                            for round in 0 .. 199 do
                                let item = pruneItems[round % pruneItems.Length]
                                sut.Add(item) |> ignore

                                if round % 2 = 0 then
                                    sut.Remove(item) |> ignore
                        else
                            for _ in 0 .. 199 do
                                for i in 0 .. keepItems.Length - 1 do
                                    sut.Add(keepItems[i]) |> ignore)
                    |> ignore

                    // Deterministic settle phase to verify the keep-prefix partition.
                    keepItems |> List.iter (fun item -> sut.Add(item) |> ignore)
                    sut.Prune(prunePrefix) |> ignore

                    let keepItemsReachable = keepItems |> List.forall sut.Contains
                    let prunePrefixRemoved = sut |> Seq.forall (fun item -> not (item.StartsWith(prunePrefix)))
                    let prunedOnlyContainsPrefix = prunedItems |> Seq.forall (fun item -> item.StartsWith(prunePrefix))

                    test <@ keepItemsReachable @>
                    test <@ prunePrefixRemoved @>
                    test <@ prunedOnlyContainsPrefix @>

                [ 13; 29; 71 ] |> List.iter runScenario
                true)

        test <@ completed @>
