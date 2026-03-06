namespace Majako.Collections.RadixTree.Tests.Dictionary

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree.Concurrent
open Majako.Collections.RadixTree.Tests
open System
open System.Collections.Concurrent
open System.Collections.Generic

type ConcurrentRadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentRadixTree<int>(items))

    let runParallelProperty work = completesWithin (TimeSpan.FromSeconds 8.0) work

    [<Property(MaxTest = 64)>]
    member _.``Parallel insertions should keep all inserted keys reachable``(keys: list<NonEmptyString>) =
        runParallelProperty (fun () ->
            let distinctKeys = keys |> List.map (fun k -> k.Get) |> List.distinct
            let sut = ConcurrentRadixTree<int>()

            [| 1 .. 8 |]
            |> Array.Parallel.iter (fun worker ->
                for key in distinctKeys do
                    sut.[key] <- worker)

            distinctKeys
            |> List.forall (fun key ->
                let found, _ = sut.TryGetValue key
                found))

    [<Property(MaxTest = 64)>]
    member _.``Parallel shared-key upserts should keep all keys reachable``(prefix: NonEmptyString, suffixes: list<NonEmptyString>) =
        runParallelProperty (fun () ->
            let keys =
                suffixes
                |> List.map (fun s -> prefix.Get + "-" + s.Get)
                |> List.distinct
                |> List.truncate 32

            let sut = ConcurrentRadixTree<int>()

            [| 1 .. 8 |]
            |> Array.Parallel.iter (fun worker ->
                for key in keys do
                    sut.[key] <- worker)

            keys
            |> List.forall (fun key ->
                let found, _ = sut.TryGetValue key
                found))

    [<Property>]
    member _.``Removing a prefix key should keep longer keys reachable and allow reinsert``
        (prefix: NonEmptyString, suffix: NonEmptyString, value1: int, value2: int)
        =
        let key = prefix.Get
        let longerKey = key + suffix.Get
        let sut = ConcurrentRadixTree<int>()

        sut.Add(key, value1)
        sut.Add(longerKey, value2)

        let removed = sut.Remove(key)
        let foundKeyAfterRemove, _ = sut.TryGetValue key
        let foundLongerAfterRemove, longerValueAfterRemove = sut.TryGetValue longerKey

        sut.[key] <- value1
        let foundKeyAfterReinsert, valueAfterReinsert = sut.TryGetValue key
        let foundLongerAfterReinsert, longerValueAfterReinsert = sut.TryGetValue longerKey

        removed
        && not foundKeyAfterRemove
        && foundLongerAfterRemove
        && longerValueAfterRemove = value2
        && foundKeyAfterReinsert
        && valueAfterReinsert = value1
        && foundLongerAfterReinsert
        && longerValueAfterReinsert = value2

    [<Property(MaxTest = 64)>]
    member _.``Parallel mixed add/remove on overlapping prefixes should keep deterministic final membership``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let keys =
                suffixes
                |> List.map (fun s -> s.Get)
                |> List.distinct
                |> List.truncate 64
                |> List.mapi (fun i s -> i, prefix.Get + "-" + s)

            let sut = ConcurrentRadixTree<int>()

            keys
            |> Array.ofList
            |> Array.Parallel.iter (fun (i, key) ->
                sut.[key] <- i

                if i % 2 = 0 then
                    sut.Remove(key) |> ignore

                if i % 4 = 0 then
                    sut.[key] <- i)

            keys
            |> List.forall (fun (i, key) ->
                let found, value = sut.TryGetValue key
                let shouldExist = i % 2 <> 0 || i % 4 = 0

                if shouldExist then
                    found && value = i
                else
                    not found))

    [<Property(MaxTest = 56)>]
    member _.``Parallel mixed operations should match reference dictionary final state``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let keys =
                suffixes
                |> List.map (fun s -> prefix.Get + "-m-" + s.Get)
                |> List.distinct
                |> List.truncate 48

            let sut = ConcurrentRadixTree<int>()
            let expected = Dictionary<string, int>()

            let applyOps (target: IDictionary<string, int>) i key =
                let baseValue = i * 17 + 3
                target[key] <- baseValue

                if i % 2 = 0 then
                    target.Remove(key) |> ignore
                else
                    target[key] <- baseValue + 1

                if i % 3 = 0 then
                    target[key] <- baseValue + 2

                if i % 5 = 0 then
                    target.Remove(key) |> ignore

            keys |> List.iteri (fun i key -> applyOps expected i key)

            keys
            |> Array.ofList
            |> Array.Parallel.iteri (fun i key ->
                let baseValue = i * 17 + 3
                sut.[key] <- baseValue

                if i % 2 = 0 then
                    sut.Remove(key) |> ignore
                else
                    sut.[key] <- baseValue + 1

                if i % 3 = 0 then
                    sut.[key] <- baseValue + 2

                if i % 5 = 0 then
                    sut.Remove(key) |> ignore)

            let expectedMap = expected |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
            let actualMap = sut |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq

            actualMap = expectedMap)

    [<Property(MaxTest = 48)>]
    member _.``Parallel remove/reinsert of a prefix key should not lose longer keys``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let prefixKey = prefix.Get

            let longerKeys =
                suffixes
                |> List.map (fun s -> s.Get)
                |> List.distinct
                |> List.truncate 16
                |> List.mapi (fun i suffix -> i, prefixKey + suffix)

            let sut = ConcurrentRadixTree<int>()
            sut.[prefixKey] <- -1

            for (i, key) in longerKeys do
                sut.[key] <- i

            [| 1 .. 8 |]
            |> Array.Parallel.iter (fun _ ->
                for _ in 1 .. 200 do
                    sut.Remove(prefixKey) |> ignore
                    sut.[prefixKey] <- 42)

            longerKeys
            |> List.forall (fun (i, key) ->
                let found, value = sut.TryGetValue key
                found && value = i))

    [<Property(MaxTest = 64)>]
    member _.``Parallel removals on overlapping prefixes should clear preloaded keys``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let keys =
                suffixes
                |> List.map (fun s -> prefix.Get + "-" + s.Get)
                |> List.distinct
                |> List.truncate 64

            let sut = ConcurrentRadixTree<int>()

            keys
            |> List.iteri (fun i key ->
                sut.[key] <- i)

            keys
            |> Array.ofList
            |> Array.Parallel.iter (fun key ->
                sut.Remove(key) |> ignore)

            keys
            |> List.forall (fun key ->
                let found, _ = sut.TryGetValue key
                not found))

    [<Property(MaxTest = 48)>]
    member _.``Parallel prune and unrelated adds should keep a valid partition``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let prunePrefix = prefix.Get + "-p-"
            let otherPrefix = prefix.Get + "-o-"

            let seeds =
                suffixes
                |> List.map (fun s -> s.Get)
                |> List.distinct
                |> List.truncate 24

            let pruneKeys = seeds |> List.map (fun s -> prunePrefix + s)
            let otherKeys = seeds |> List.map (fun s -> otherPrefix + s)
            let sut = ConcurrentRadixTree<int>()
            let prunedKeys = ConcurrentBag<string>()

            pruneKeys |> List.iteri (fun i key -> sut.[key] <- i)
            otherKeys |> List.iteri (fun i key -> sut.[key] <- i)

            [| 1 .. 2 |]
            |> Array.Parallel.iter (fun worker ->
                if worker = 1 then
                    for _ in 1 .. 20 do
                        let pruned = sut.Prune(prunePrefix)
                        for kv in pruned do
                            prunedKeys.Add(kv.Key)
                else
                    for i in 1 .. 200 do
                        for key in otherKeys do
                            sut.[key] <- i)

            let prunedAreValid = prunedKeys |> Seq.forall (fun k -> k.StartsWith(prunePrefix))
            let remainingAreValid = sut.Keys |> Seq.forall (fun k -> not (k.StartsWith(prunePrefix)))
            let othersReachable = otherKeys |> List.forall sut.ContainsKey

            prunedAreValid && remainingAreValid && othersReachable)

    [<Property(MaxTest = 24)>]
    member _.``Parallel round-based mixed operations should match reference dictionary model``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let seeds =
                suffixes
                |> List.map (fun s -> s.Get)
                |> List.distinct
                |> List.truncate 20

            if List.isEmpty seeds then
                true
            else
                let prunePrefix = prefix.Get + "-pm-"
                let keepPrefix = prefix.Get + "-km-"
                let pruneKeys = seeds |> List.map (fun s -> prunePrefix + s)
                let keepKeys = seeds |> List.map (fun s -> keepPrefix + s)
                let sut = ConcurrentRadixTree<int>()
                let expected = Dictionary<string, int>()

                pruneKeys |> List.iteri (fun i key -> sut.[key] <- -(i + 1); expected[key] <- -(i + 1))
                keepKeys |> List.iteri (fun i key -> sut.[key] <- i; expected[key] <- i)

                for round in 0 .. 15 do
                    let keepOps =
                        keepKeys
                        |> List.mapi (fun i key -> i, key, round * 1000 + i)

                    keepOps
                    |> List.iter (fun (i, key, value) ->
                        if (round + i) % 4 = 0 then
                            expected.Remove(key) |> ignore
                        else
                            expected[key] <- value)

                    keepOps
                    |> Array.ofList
                    |> Array.Parallel.iter (fun (i, key, value) ->
                        if (round + i) % 4 = 0 then
                            sut.Remove(key) |> ignore
                        else
                            sut.[key] <- value)

                    if round % 2 = 0 then
                        let pruneOps =
                            pruneKeys
                            |> List.mapi (fun i key -> i, key, round * 2000 + i)

                        pruneOps
                        |> List.iter (fun (i, key, value) ->
                            if (round + i) % 3 = 0 then
                                expected.Remove(key) |> ignore
                            else
                                expected[key] <- value)

                        pruneOps
                        |> Array.ofList
                        |> Array.Parallel.iter (fun (i, key, value) ->
                            if (round + i) % 3 = 0 then
                                sut.Remove(key) |> ignore
                            else
                                sut.[key] <- value)
                    else
                        let keepBurst =
                            keepKeys
                            |> List.mapi (fun i key -> key, round * 3000 + i)

                        keepBurst |> List.iter (fun (key, value) -> expected[key] <- value)

                        let prunable =
                            expected.Keys
                            |> Seq.filter (fun key -> key.StartsWith(prunePrefix))
                            |> Seq.toArray

                        prunable |> Array.iter (fun key -> expected.Remove(key) |> ignore)

                        [| 0; 1 |]
                        |> Array.Parallel.iter (fun worker ->
                            if worker = 0 then
                                sut.Prune(prunePrefix) |> ignore
                            else
                                for (key, value) in keepBurst do
                                    sut.[key] <- value)

                // Deterministic settle phase.
                keepKeys |> List.iteri (fun i key -> expected[key] <- i; sut.[key] <- i)
                sut.Prune(prunePrefix) |> ignore

                expected.Keys
                |> Seq.filter (fun key -> key.StartsWith(prunePrefix))
                |> Seq.toArray
                |> Array.iter (fun key -> expected.Remove(key) |> ignore)

                let expectedMap = expected |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
                let actualMap = sut |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
                actualMap = expectedMap)
