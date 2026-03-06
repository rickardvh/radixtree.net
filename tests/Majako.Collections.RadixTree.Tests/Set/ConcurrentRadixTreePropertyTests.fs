namespace Majako.Collections.RadixTree.Tests.Set

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree.Concurrent
open System.Collections.Concurrent

type ConcurrentRadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentRadixTree(items))

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

    [<Property>]
    member _.``Parallel shared-item adds should keep all items reachable``(prefix: NonEmptyString, suffixes: list<NonEmptyString>) =
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

        items |> List.forall sut.Contains

    [<Property>]
    member _.``Parallel mixed add/remove on overlapping prefixes should keep deterministic final membership``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
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
            sut.Contains(item) = shouldExist)

    [<Property>]
    member _.``Parallel remove/re-add of a prefix item should not lose longer items``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
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

        longerItems |> List.forall sut.Contains

    [<Property>]
    member _.``Parallel removals on overlapping prefixes should clear preloaded items``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
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

        items |> List.forall (sut.Contains >> not)

    [<Property>]
    member _.``Parallel prune and unrelated adds should keep a valid partition``
        (prefix: NonEmptyString, suffixes: list<NonEmptyString>)
        =
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

        prunedAreValid && remainingAreValid && othersReachable
