namespace Majako.Collections.RadixTree.Tests.Set

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree
open System.Collections.Generic
open System
open Majako.Collections.RadixTree.Tests

type TestItems = list<NonEmptyString>

[<AbstractClass>]
type PrefixTreePropertyTestBase(ctor: (string seq -> IPrefixTree)) =
    let makeTrie (items: TestItems) =
        ctor (items |> List.map (fun i -> i.Get))

    [<Property>]
    let ``An item should exist after being added`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        sut.Add item.Get |> ignore
        sut.Contains item.Get

    [<Property>]
    let ``Adding one item should increase count by exactly 1 if it is not in the trie, and 0 otherwise``
        (items: TestItems, item: NonEmptyString)
        =
        let sut = makeTrie items
        let countBefore = sut.Count
        let containsItemBefore = sut.Contains item.Get
        sut.Add item.Get |> ignore
        let countAfter = sut.Count

        containsItemBefore && countBefore = countAfter
        || not containsItemBefore && countBefore + 1 = countAfter

    [<Property>]
    let ``Removing one item should decrease count by exactly 1 if it is in the trie, and 0 otherwise``
        (items: TestItems, item: NonEmptyString)
        =
        let sut = makeTrie items
        let countBefore = sut.Count
        let containsItemBefore = sut.Contains item.Get
        sut.Remove item.Get |> ignore
        let countAfter = sut.Count

        not containsItemBefore && countBefore = countAfter
        || containsItemBefore && countBefore - 1 = countAfter

    [<Property>]
    let ``Remove should be idempotent`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        sut.Remove item.Get |> ignore
        let before = List sut
        sut.Remove item.Get |> ignore
        sut == before

    [<Property>]
    let ``Add should be idempotent`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        sut.Add item.Get |> ignore
        let before = List sut
        sut.Add item.Get |> ignore
        sut == before

    [<Property>]
    let ``Prune should be idempotent`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        sut.Prune item.Get |> ignore
        let before = List sut
        sut.Prune item.Get |> ignore
        sut == before

    [<Property>]
    let ``An item should not exist after being added and removed`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        sut.Add item.Get |> ignore
        sut.Remove item.Get |> ignore
        not <| sut.Contains item.Get

    [<Property>]
    let ``An item should not exist after being removed`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        sut.Remove(item.Get) |> ignore
        not <| sut.Contains item.Get

    [<Property>]
    let ``A item should exist after being added and removed and added again``
        (items: TestItems, item: NonEmptyString, value: int)
        =
        let sut = makeTrie items
        sut.Add item.Get |> ignore
        sut.Remove item.Get |> ignore
        sut.Add item.Get |> ignore
        sut.Contains item.Get

    [<Property>]
    let ``Items should be the same as the ones added`` (items: Set<NonEmptyString>) =
        let sut = RadixTree()

        for item in items do
            sut.Add item.Get |> ignore

        let expected = items |> Seq.map (fun k -> k.Get)
        sut == expected

    [<Property>]
    let ``Count should equal the number of distinct items`` (items: TestItems) =
        let sut = makeTrie items
        let expectedCount = items |> Seq.map (fun x -> x.Get) |> Set.ofSeq |> Set.count
        sut.Count = expectedCount

    [<Property>]
    let ``Add return value and final count should match HashSet`` (items: TestItems) =
        let sut = ctor Seq.empty
        let expected = HashSet<string>()

        let addResultsMatch =
            items
            |> List.forall (fun item ->
                let key = item.Get
                let expectedAdded = expected.Add(key)
                let actualAdded = sut.Add(key)
                expectedAdded = actualAdded)

        addResultsMatch && sut.Count = expected.Count

    [<Property>]
    let ``A pruned subtree should contain only items with the given prefix`` (items: TestItems, prefix: string) =
        let sut = makeTrie items
        let subTree = sut.Prune prefix
        subTree |> Seq.forall (fun k -> startsWith k prefix)

    [<Property>]
    let ``Pruning should remove all items with the given prefix`` (items: TestItems, prefix: string) =
        let sut = makeTrie items
        sut.Prune prefix |> ignore
        sut |> Seq.forall (fun k -> not (startsWith k prefix))

    [<Property>]
    let ``Clearing the tree should remove all items`` (items: TestItems) =
        let sut = makeTrie items
        sut.Clear()
        Seq.isEmpty sut

    [<Property>]
    let ``ExceptWith self should clear the set without throwing`` (items: TestItems) =
        let sut = makeTrie items

        try
            sut.ExceptWith(sut)
            Seq.isEmpty sut
        with _ ->
            false

    [<Property>]
    let ``CopyTo should throw ArgumentException when destination is too small`` (items: TestItems, arrayIndex: NonNegativeInt) =
        let sut = makeTrie items
        let count = sut.Count

        if count = 0 then
            true
        else
            let index = arrayIndex.Get % 3
            let arr = Array.zeroCreate<string> (index + count - 1)
            try
                sut.CopyTo(arr, index)
                false
            with
            | :? ArgumentException -> true
            | _ -> false

    [<Property>]
    let ``SymmetricExceptWith should match HashSet behavior`` (items: TestItems, other: TestItems) =
        let sut = makeTrie items
        let expected = HashSet(items |> Seq.map (fun x -> x.Get))
        let otherValues = other |> Seq.map (fun x -> x.Get) |> Seq.toList

        expected.SymmetricExceptWith(otherValues)
        sut.SymmetricExceptWith(otherValues)

        sut == expected

    [<Property>]
    let ``Prune should partition items by prefix`` (items: TestItems, prefix: string) =
        let sut = makeTrie items
        let expected = items |> Seq.map (fun x -> x.Get) |> Set.ofSeq
        let subtree = sut.Prune(prefix)

        let expectedPruned = expected |> Seq.filter (fun k -> startsWith k prefix) |> Set.ofSeq
        let expectedRemaining = expected |> Seq.filter (fun k -> not (startsWith k prefix)) |> Set.ofSeq
        let actualPruned = subtree |> Set.ofSeq
        let actualRemaining = sut |> Set.ofSeq

        actualPruned = expectedPruned && actualRemaining = expectedRemaining

    [<Property>]
    let ``Set operations should throw ArgumentNullException when other is null`` (items: TestItems) =
        let sut = makeTrie items
        let isArgNull f =
            try
                f ()
                false
            with
            | :? ArgumentNullException -> true
            | _ -> false

        isArgNull (fun () -> sut.IntersectWith null)
        && isArgNull (fun () -> sut.IsProperSubsetOf null |> ignore)
        && isArgNull (fun () -> sut.IsProperSupersetOf null |> ignore)
        && isArgNull (fun () -> sut.IsSubsetOf null |> ignore)
        && isArgNull (fun () -> sut.IsSupersetOf null |> ignore)
        && isArgNull (fun () -> sut.ExceptWith null)
        && isArgNull (fun () -> sut.Overlaps null |> ignore)
        && isArgNull (fun () -> sut.SetEquals null |> ignore)
        && isArgNull (fun () -> sut.SymmetricExceptWith null)
        && isArgNull (fun () -> sut.UnionWith null)
