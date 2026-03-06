namespace Majako.Collections.RadixTree.Tests.Set

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree.Concurrent
open Majako.Collections.RadixTree.Tests
open System
open System.Collections.Concurrent
open System.Collections.Generic

type ConcurrentRadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentRadixTree(items))

    let runParallelProperty work = completesWithin (TimeSpan.FromSeconds 8.0) work

    [<Property>]
    member _.``Removing a prefix item should keep longer items reachable and allow re-add``
        (prefix: NonEmptyString, suffix: NonEmptyString)
        =
        let item = prefix.Get
        let longerItem = item + suffix.Get
        let sut = ConcurrentRadixTree()

        sut.Add(item) |> ignore
        sut.Add(longerItem) |> ignore

        let removed = sut.Remove(item)
        let hasItemAfterRemove = sut.Contains(item)
        let hasLongerAfterRemove = sut.Contains(longerItem)

        sut.Add(item) |> ignore
        let hasItemAfterReadd = sut.Contains(item)
        let hasLongerAfterReadd = sut.Contains(longerItem)

        removed
        && not hasItemAfterRemove
        && hasLongerAfterRemove
        && hasItemAfterReadd
        && hasLongerAfterReadd

    [<Property(MaxTest = 64)>]
    member _.``Parallel shared-item adds should keep all items reachable``(prefix: NonEmptyString, suffixes: list<NonEmptyString>) =
        runParallelProperty (fun () ->
            let items =
                suffixes
                |> List.map (fun s -> prefix.Get + "-" + s.Get)
                |> List.distinct
                |> List.truncate 32

            let sut = ConcurrentRadixTree()

            [| 1 .. 8 |]
            |> Array.Parallel.iter (fun _ ->
                for item in items do
                    sut.Add(item) |> ignore)

            items |> List.forall sut.Contains)

    [<Property(MaxTest = 64)>]
    member _.``Parallel mixed add/remove on overlapping prefixes should keep deterministic final membership``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let items =
                suffixes
                |> List.map (fun s -> s.Get)
                |> List.distinct
                |> List.truncate 64
                |> List.mapi (fun i s -> i, prefix.Get + "-" + s)

            let sut = ConcurrentRadixTree()

            items
            |> Array.ofList
            |> Array.Parallel.iter (fun (i, item) ->
                sut.Add(item) |> ignore

                if i % 2 = 0 then
                    sut.Remove(item) |> ignore

                if i % 4 = 0 then
                    sut.Add(item) |> ignore)

            items
            |> List.forall (fun (i, item) ->
                let shouldExist = i % 2 <> 0 || i % 4 = 0
                sut.Contains(item) = shouldExist))

    [<Property(MaxTest = 56)>]
    member _.``Parallel mixed operations should match reference hash set final state``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let items =
                suffixes
                |> List.map (fun s -> prefix.Get + "-m-" + s.Get)
                |> List.distinct
                |> List.truncate 48

            let sut = ConcurrentRadixTree()
            let expected = HashSet<string>()

            let applyOps (target: HashSet<string>) i item =
                target.Add(item) |> ignore

                if i % 2 = 0 then
                    target.Remove(item) |> ignore
                else
                    target.Add(item) |> ignore

                if i % 3 = 0 then
                    target.Add(item) |> ignore

                if i % 5 = 0 then
                    target.Remove(item) |> ignore

            items |> List.iteri (fun i item -> applyOps expected i item)

            items
            |> Array.ofList
            |> Array.Parallel.iteri (fun i item ->
                sut.Add(item) |> ignore

                if i % 2 = 0 then
                    sut.Remove(item) |> ignore
                else
                    sut.Add(item) |> ignore

                if i % 3 = 0 then
                    sut.Add(item) |> ignore

                if i % 5 = 0 then
                    sut.Remove(item) |> ignore)

            let actual = sut |> Set.ofSeq
            let expectedSet = expected |> Set.ofSeq
            actual = expectedSet)

    [<Property(MaxTest = 48)>]
    member _.``Parallel remove/re-add of a prefix item should not lose longer items``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let prefixItem = prefix.Get

            let longerItems =
                suffixes
                |> List.map (fun s -> s.Get)
                |> List.distinct
                |> List.truncate 16
                |> List.map (fun suffix -> prefixItem + suffix)

            let sut = ConcurrentRadixTree()
            sut.Add(prefixItem) |> ignore

            for item in longerItems do
                sut.Add(item) |> ignore

            [| 1 .. 8 |]
            |> Array.Parallel.iter (fun _ ->
                for _ in 1 .. 200 do
                    sut.Remove(prefixItem) |> ignore
                    sut.Add(prefixItem) |> ignore)

            longerItems |> List.forall sut.Contains)

    [<Property(MaxTest = 64)>]
    member _.``Parallel removals on overlapping prefixes should clear preloaded items``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
        runParallelProperty (fun () ->
            let items =
                suffixes
                |> List.map (fun s -> prefix.Get + "-" + s.Get)
                |> List.distinct
                |> List.truncate 64

            let sut = ConcurrentRadixTree()

            for item in items do
                sut.Add(item) |> ignore

            items
            |> Array.ofList
            |> Array.Parallel.iter (fun item ->
                sut.Remove(item) |> ignore)

            items |> List.forall (sut.Contains >> not))

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

            let pruneItems = seeds |> List.map (fun s -> prunePrefix + s)
            let otherItems = seeds |> List.map (fun s -> otherPrefix + s)
            let sut = ConcurrentRadixTree()
            let prunedItems = ConcurrentBag<string>()

            for item in pruneItems do
                sut.Add(item) |> ignore

            for item in otherItems do
                sut.Add(item) |> ignore

            [| 1 .. 2 |]
            |> Array.Parallel.iter (fun worker ->
                if worker = 1 then
                    for _ in 1 .. 20 do
                        let pruned = sut.Prune(prunePrefix)
                        for item in pruned do
                            prunedItems.Add(item)
                else
                    for _ in 1 .. 200 do
                        for item in otherItems do
                            sut.Add(item) |> ignore)

            let prunedAreValid = prunedItems |> Seq.forall (fun item -> item.StartsWith(prunePrefix))
            let remainingAreValid = sut |> Seq.forall (fun item -> not (item.StartsWith(prunePrefix)))
            let othersReachable = otherItems |> List.forall sut.Contains

            prunedAreValid && remainingAreValid && othersReachable)

    [<Property(MaxTest = 24)>]
    member _.``Parallel round-based mixed operations should match reference hash set model``
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
                let pruneItems = seeds |> List.map (fun s -> prunePrefix + s)
                let keepItems = seeds |> List.map (fun s -> keepPrefix + s)
                let sut = ConcurrentRadixTree()
                let expected = HashSet<string>()

                pruneItems |> List.iter (fun item -> sut.Add(item) |> ignore; expected.Add(item) |> ignore)
                keepItems |> List.iter (fun item -> sut.Add(item) |> ignore; expected.Add(item) |> ignore)

                for round in 0 .. 15 do
                    let keepOps = keepItems |> List.mapi (fun i item -> i, item)

                    keepOps
                    |> List.iter (fun (i, item) ->
                        if (round + i) % 4 = 0 then
                            expected.Remove(item) |> ignore
                        else
                            expected.Add(item) |> ignore)

                    keepOps
                    |> Array.ofList
                    |> Array.Parallel.iter (fun (i, item) ->
                        if (round + i) % 4 = 0 then
                            sut.Remove(item) |> ignore
                        else
                            sut.Add(item) |> ignore)

                    if round % 2 = 0 then
                        let pruneOps = pruneItems |> List.mapi (fun i item -> i, item)

                        pruneOps
                        |> List.iter (fun (i, item) ->
                            if (round + i) % 3 = 0 then
                                expected.Remove(item) |> ignore
                            else
                                expected.Add(item) |> ignore)

                        pruneOps
                        |> Array.ofList
                        |> Array.Parallel.iter (fun (i, item) ->
                            if (round + i) % 3 = 0 then
                                sut.Remove(item) |> ignore
                            else
                                sut.Add(item) |> ignore)
                    else
                        keepItems |> List.iter (fun item -> expected.Add(item) |> ignore)

                        expected
                        |> Seq.filter (fun item -> item.StartsWith(prunePrefix))
                        |> Seq.toArray
                        |> Array.iter (fun item -> expected.Remove(item) |> ignore)

                        [| 0; 1 |]
                        |> Array.Parallel.iter (fun worker ->
                            if worker = 0 then
                                sut.Prune(prunePrefix) |> ignore
                            else
                                for item in keepItems do
                                    sut.Add(item) |> ignore)

                // Deterministic settle phase.
                keepItems |> List.iter (fun item -> expected.Add(item) |> ignore; sut.Add(item) |> ignore)
                sut.Prune(prunePrefix) |> ignore

                expected
                |> Seq.filter (fun item -> item.StartsWith(prunePrefix))
                |> Seq.toArray
                |> Array.iter (fun item -> expected.Remove(item) |> ignore)

                let expectedSet = expected |> Set.ofSeq
                let actualSet = sut |> Set.ofSeq
                actualSet = expectedSet)
