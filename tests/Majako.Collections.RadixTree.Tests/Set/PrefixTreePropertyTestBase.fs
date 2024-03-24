namespace Majako.Collections.RadixTree.Tests.Set

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree
open System.Collections.Generic
open Majako.Collections.RadixTree.Tests

type TestItems = list<NonEmptyString>

[<AbstractClass>]
type PrefixTreePropertyTestBase(ctor: (string seq -> IPrefixTree)) =
    let makeTrie (items: TestItems) =
        ctor (items |> List.map (fun i -> i.Get))

    [<Property>]
    let ``An item should exist after being added`` (items: TestItems, item: NonEmptyString) =
        let sut = makeTrie items
        (sut.Add item.Get) && (sut.Contains item.Get)

    [<Property>]
    let ``Adding one item should increase count by exactly 1 if it is not in the trie, and 0 otherwise``
        (
            items: TestItems,
            item: NonEmptyString
        ) =
        let sut = makeTrie items
        let countBefore = sut.Count
        let containsItemBefore = sut.Contains item.Get
        sut.Add item.Get |> ignore
        let countAfter = sut.Count
        containsItemBefore && countBefore = countAfter || not containsItemBefore && countBefore + 1 = countAfter

    [<Property>]
    let ``Removing one item should decrease count by exactly 1 if it is in the trie, and 0 otherwise``
        (
            items: TestItems,
            item: NonEmptyString
        ) =
        let sut = makeTrie items
        let countBefore = sut.Count
        let containsItemBefore = sut.Contains item.Get
        sut.Remove item.Get |> ignore
        let countAfter = sut.Count
        not containsItemBefore && countBefore = countAfter || containsItemBefore && countBefore - 1 = countAfter

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
    let ``An item should not exist after being added and removed``
        (
            items: TestItems,
            item: NonEmptyString
        ) =
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
        (
            items: TestItems,
            item: NonEmptyString,
            value: int
        ) =
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
