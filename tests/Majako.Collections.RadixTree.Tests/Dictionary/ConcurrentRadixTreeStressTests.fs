namespace Majako.Collections.RadixTree.Tests.Dictionary

open Majako.Collections.RadixTree.Concurrent
open Majako.Collections.RadixTree.Tests
open Swensen.Unquote
open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading.Tasks
open Xunit

type ConcurrentRadixTreeStressTests() =
    let buildKeys seed prefix count =
        let rng = Random(seed)

        [ for i in 0 .. count - 1 ->
              let shard = i % 16
              let salt = rng.Next(0, 4096)
              $"{prefix}{shard}-{salt:D4}-{i:D4}" ]

    let stressSeeds = [ 13; 29; 43; 71; 97 ]

    [<Fact>]
    [<Trait("Category", "Stress")>]
    member _.``Deterministic mixed parallel workloads should match reference dictionary``() =
        let completed =
            completesWithin (TimeSpan.FromSeconds 45.0) (fun () ->
                let runScenario seed =
                    let keys = buildKeys seed $"k-{seed}-" 256
                    let sut = ConcurrentRadixTree<int>()
                    let expected = Dictionary<string, int>()

                    let applyOps (target: IDictionary<string, int>) i key =
                        let baseValue = seed * 1000 + i
                        target[key] <- baseValue

                        if (i + seed) % 2 = 0 then
                            target.Remove(key) |> ignore
                        else
                            target[key] <- baseValue + 1

                        if (i + seed) % 3 = 0 then
                            target[key] <- baseValue + 2

                        if (i + seed) % 5 = 0 then
                            target.Remove(key) |> ignore

                    keys |> List.iteri (fun i key -> applyOps expected i key)

                    let options = ParallelOptions(MaxDegreeOfParallelism = 8)

                    Parallel.For(0, keys.Length, options, fun i ->
                        let key = keys[i]
                        let baseValue = seed * 1000 + i

                        sut.[key] <- baseValue

                        if (i + seed) % 2 = 0 then
                            sut.Remove(key) |> ignore
                        else
                            sut.[key] <- baseValue + 1

                        if (i + seed) % 3 = 0 then
                            sut.[key] <- baseValue + 2

                        if (i + seed) % 5 = 0 then
                            sut.Remove(key) |> ignore)
                    |> ignore

                    let actualMap = sut |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
                    let expectedMap = expected |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
                    test <@ actualMap = expectedMap @>

                stressSeeds |> List.iter runScenario
                true)

        test <@ completed @>

    [<Fact>]
    [<Trait("Category", "Stress")>]
    member _.``Deterministic prune-add-remove races should preserve keep-prefix keys``() =
        let completed =
            completesWithin (TimeSpan.FromSeconds 45.0) (fun () ->
                let runScenario seed =
                    let prunePrefix = $"p-{seed}-"
                    let keepPrefix = $"k-{seed}-"
                    let pruneKeys = buildKeys (seed + 11) prunePrefix 96
                    let keepKeys = buildKeys (seed + 23) keepPrefix 96
                    let sut = ConcurrentRadixTree<int>()
                    let prunedKeys = ConcurrentBag<string>()

                    pruneKeys |> List.iteri (fun i key -> sut.[key] <- i)
                    keepKeys |> List.iteri (fun i key -> sut.[key] <- i)

                    let options = ParallelOptions(MaxDegreeOfParallelism = 6)

                    Parallel.For(0, 6, options, fun worker ->
                        if worker = 0 then
                            for round in 0 .. 199 do
                                let subtree = sut.Prune(prunePrefix)
                                for kv in subtree do
                                    prunedKeys.Add(kv.Key)

                                if round % 3 = 0 then
                                    for i in 0 .. 15 do
                                        let key = pruneKeys[(round + i) % pruneKeys.Length]
                                        sut.[key] <- round + i
                        elif worker = 1 then
                            for round in 0 .. 199 do
                                let key = pruneKeys[round % pruneKeys.Length]
                                sut.[key] <- round

                                if round % 2 = 0 then
                                    sut.Remove(key) |> ignore
                        else
                            for round in 0 .. 199 do
                                for i in 0 .. keepKeys.Length - 1 do
                                    sut.[keepKeys[i]] <- worker * 100_000 + round)
                    |> ignore

                    // Deterministic settle phase to verify the keep-prefix partition.
                    keepKeys |> List.iteri (fun i key -> sut.[key] <- i)
                    sut.Prune(prunePrefix) |> ignore

                    let keepKeysReachable =
                        keepKeys
                        |> List.forall (fun key ->
                            let found, _ = sut.TryGetValue key
                            found)

                    let prunePrefixRemoved = sut.Keys |> Seq.forall (fun key -> not (key.StartsWith(prunePrefix)))
                    let prunedOnlyContainsPrefix = prunedKeys |> Seq.forall (fun key -> key.StartsWith(prunePrefix))

                    test <@ keepKeysReachable @>
                    test <@ prunePrefixRemoved @>
                    test <@ prunedOnlyContainsPrefix @>

                stressSeeds |> List.iter runScenario
                true)

        test <@ completed @>

    [<Fact>]
    [<Trait("Category", "Stress")>]
    member _.``Deterministic seeded high-churn races should preserve keep-prefix partition after settle``() =
        let completed =
            completesWithin (TimeSpan.FromSeconds 60.0) (fun () ->
                let runScenario seed =
                    let churnPrefix = $"c-{seed}-"
                    let keepPrefix = $"k-{seed}-"
                    let churnKeys = buildKeys (seed + 101) churnPrefix 160
                    let keepKeys = buildKeys (seed + 211) keepPrefix 160
                    let sut = ConcurrentRadixTree<int>()

                    churnKeys |> List.iteri (fun i key -> sut.[key] <- -(i + 1))
                    keepKeys |> List.iteri (fun i key -> sut.[key] <- i)

                    let options = ParallelOptions(MaxDegreeOfParallelism = 8)

                    Parallel.For(0, 8, options, fun worker ->
                        let rng = Random(seed * 1000 + worker)

                        if worker = 0 then
                            for round in 0 .. 299 do
                                let subtree = sut.Prune(churnPrefix)
                                if round % 4 = 0 then
                                    for i in 0 .. 7 do
                                        let key = churnKeys[(round + i) % churnKeys.Length]
                                        sut.[key] <- round + i

                                if round % 9 = 0 then
                                    for kv in subtree do
                                        if kv.Key.GetHashCode() % 5 = 0 then
                                            sut.[kv.Key] <- kv.Value
                        elif worker % 2 = 0 then
                            for round in 0 .. 399 do
                                let key = churnKeys[rng.Next(churnKeys.Length)]
                                sut.[key] <- worker * 100_000 + round

                                if round % 3 = 0 then
                                    sut.Remove(key) |> ignore
                        else
                            for round in 0 .. 399 do
                                let key = keepKeys[rng.Next(keepKeys.Length)]
                                sut.[key] <- worker * 100_000 + round)
                    |> ignore

                    // Deterministic settle phase.
                    keepKeys |> List.iteri (fun i key -> sut.[key] <- i)
                    sut.Prune(churnPrefix) |> ignore

                    let expectedValues = keepKeys |> List.mapi (fun i key -> key, i) |> Map.ofList

                    let keepPartitionIntact =
                        keepKeys
                        |> List.forall (fun key ->
                            let found, value = sut.TryGetValue key
                            found && value = expectedValues[key])

                    let churnPrefixRemoved = sut.Keys |> Seq.forall (fun key -> not (key.StartsWith(churnPrefix)))

                    test <@ keepPartitionIntact @>
                    test <@ churnPrefixRemoved @>

                stressSeeds |> List.iter runScenario
                true)

        test <@ completed @>
