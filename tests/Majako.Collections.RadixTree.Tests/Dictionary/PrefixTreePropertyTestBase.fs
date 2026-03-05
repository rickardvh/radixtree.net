namespace Majako.Collections.RadixTree.Tests.Dictionary

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree
open System.Collections.Generic
open System
open Majako.Collections.RadixTree.Tests

type TestItems = list<NonEmptyString * int>

[<AbstractClass>]
type PrefixTreePropertyTestBase(ctor: (KeyValuePair<string, int> seq -> IPrefixTree<int>)) =
    let makeTrie (items: TestItems) =
        ctor (items |> List.map (fun (k, v) -> KeyValuePair(k.Get, v)))

    [<Property>]
    let ``A key/value pair should exist after being added`` (items: TestItems, key: NonEmptyString, value: int) =
        let sut = makeTrie items
        let foundBefore, valueBefore = sut.TryGetValue key.Get

        let threw =
            try
                sut.Add(key.Get, value)
                false
            with
            | :? ArgumentException -> true
            | _ -> false

        let foundAfter, valueAfter = sut.TryGetValue key.Get

        if foundBefore then
            threw && foundAfter && valueAfter = valueBefore
        else
            not threw && foundAfter && valueAfter = value

    [<Property>]
    let ``Adding one value should increase count by exactly 1 if the key is not in the trie, and 0 otherwise``
        (items: TestItems, key: NonEmptyString, value: int)
        =
        let sut = makeTrie items
        let countBefore = sut.Count
        let containsKeyBefore = sut.ContainsKey key.Get
        let threw =
            try
                sut.Add(key.Get, value)
                false
            with
            | :? ArgumentException -> true
            | _ -> false
        let countAfter = sut.Count

        containsKeyBefore && threw && countBefore = countAfter
        || not containsKeyBefore && not threw && countBefore + 1 = countAfter

    [<Property>]
    let ``Removing one value should decrease count by exactly 1 if the key is in the trie, and 0 otherwise``
        (items: TestItems, key: NonEmptyString)
        =
        let sut = makeTrie items
        let countBefore = sut.Count
        let containsKeyBefore = sut.ContainsKey key.Get
        sut.Remove key.Get |> ignore
        let countAfter = sut.Count

        not containsKeyBefore && countBefore = countAfter
        || containsKeyBefore && countBefore - 1 = countAfter

    [<Property>]
    let ``Remove should be idempotent`` (items: TestItems, key: NonEmptyString) =
        let sut = makeTrie items
        sut.Remove key.Get |> ignore
        let before = sut.Count
        sut.Remove key.Get |> ignore
        let countAfter = sut.Count
        before = countAfter

    [<Property>]
    let ``Adding the same key twice should throw and keep count unchanged`` (key: NonEmptyString, value: int) =
        let sut = ctor Seq.empty
        sut.Add(key.Get, value)
        let countBefore = sut.Count
        let threw =
            try
                sut.Add(key.Get, value)
                false
            with
            | :? ArgumentException -> true
            | _ -> false
        let countAfter = sut.Count
        threw && countBefore = countAfter

    [<Property>]
    let ``Adding a duplicate key should throw and keep the original value`` (key: NonEmptyString, value1: int, value2: int) =
        let sut = ctor Seq.empty
        sut.Add(key.Get, value1)

        let threw =
            try
                sut.Add(key.Get, value2)
                false
            with
            | :? ArgumentException -> true
            | _ -> false

        let found, actual = sut.TryGetValue key.Get

        threw && found && actual = value1

    [<Property>]
    let ``Prune should be idempotent`` (items: TestItems, key: NonEmptyString) =
        let sut = makeTrie items
        sut.Prune key.Get |> ignore
        let countBefore = sut.Count
        sut.Prune key.Get |> ignore
        let countAfter = sut.Count
        countBefore = countAfter

    [<Property>]
    let ``A key/value pair should not exist after being added and removed``
        (items: TestItems, key: NonEmptyString, value: int)
        =
        let sut = makeTrie items
        sut.Remove key.Get |> ignore
        sut.Add(key.Get, value)
        sut.Remove key.Get |> ignore
        let found, _ = sut.TryGetValue key.Get
        not found

    [<Property>]
    let ``A key/value pair should not exist after being removed`` (items: TestItems, key: NonEmptyString) =
        let sut = makeTrie items
        sut.Remove key.Get |> ignore
        let found, _ = sut.TryGetValue key.Get
        not found

    [<Property>]
    let ``A key/value pair should exist after being added and removed and added again``
        (items: TestItems, key: NonEmptyString, value: int)
        =
        let sut = makeTrie items
        sut.Remove key.Get |> ignore
        sut.Add(key.Get, value)
        sut.Remove key.Get |> ignore
        sut.Add(key.Get, value)
        let found, actual = sut.TryGetValue key.Get
        found && actual = value

    [<Property>]
    let ``A key/value pair should exist after being added and removed and added again with a different value``
        (items: TestItems, key: NonEmptyString, value: int, value2: int)
        =
        let sut = makeTrie items
        sut.Remove key.Get |> ignore
        sut.Add(key.Get, value)
        sut.Remove key.Get |> ignore
        sut.Add(key.Get, value2)
        let found, actual = sut.TryGetValue key.Get
        found && actual = value2

    [<Property>]
    let ``Keys should be the same as the ones added`` (keys: Set<NonEmptyString>) =
        let sut = RadixTree<int>()

        for key in keys do
            sut.Add(key.Get, 0)

        let expected = keys |> Seq.map (fun k -> k.Get)
        sut.Keys == expected

    [<Property>]
    let ``Count should equal the number of distinct keys after upserts`` (items: TestItems) =
        let sut = ctor Seq.empty
        let expected = HashSet<string>()

        for (key, value) in items do
            sut[key.Get] <- value
            expected.Add(key.Get) |> ignore

        sut.Count = expected.Count

    [<Property>]
    let ``A pruned subtree should contain only keys with the given prefix`` (items: TestItems, prefix: string) =
        let sut = makeTrie items
        let subTree = sut.Prune prefix
        subTree.Keys |> Seq.forall (fun k -> startsWith k prefix)

    [<Property>]
    let ``Pruning should remove all keys with the given prefix`` (items: TestItems, prefix: string) =
        let sut = makeTrie items
        sut.Prune(prefix) |> ignore
        sut.Keys |> Seq.forall (fun k -> not (startsWith k prefix))

    [<Property>]
    let ``Clearing the tree should remove all keys`` (items: TestItems) =
        let sut = makeTrie items
        sut.Clear()
        Seq.isEmpty sut.Keys

    [<Property>]
    let ``CopyTo should throw ArgumentException when destination is too small`` (items: TestItems, arrayIndex: NonNegativeInt) =
        let sut = makeTrie items
        let count = sut.Count

        if count = 0 then
            true
        else
            let index = arrayIndex.Get % 3
            let arr = Array.zeroCreate<KeyValuePair<string, int>> (index + count - 1)
            try
                sut.CopyTo(arr, index)
                false
            with
            | :? ArgumentException -> true
            | _ -> false

    [<Property>]
    let ``Removing one of two sibling keys should report success`` (prefix: NonEmptyString, seed: int) =
        let sut = ctor Seq.empty
        let c1 = char (int 'a' + (abs seed % 26))
        let c2 = char (int 'a' + ((abs seed + 1) % 26))
        let k1 = prefix.Get + string c1
        let k2 = prefix.Get + string c2

        sut.Add(k1, 1)
        sut.Add(k2, 2)

        let removed = sut.Remove(k1)
        let found, _ = sut.TryGetValue(k1)

        removed && not found

    [<Property>]
    let ``Prune should partition key-value pairs by prefix`` (items: TestItems, prefix: string) =
        let sut = makeTrie items
        let expected = System.Collections.Generic.Dictionary<string, int>()

        for (key, value) in items do
            expected[key.Get] <- value

        let subtree = sut.Prune(prefix)

        let expectedPruned =
            expected
            |> Seq.filter (fun kv -> startsWith kv.Key prefix)
            |> Seq.map (fun kv -> kv.Key, kv.Value)
            |> Map.ofSeq

        let expectedRemaining =
            expected
            |> Seq.filter (fun kv -> not (startsWith kv.Key prefix))
            |> Seq.map (fun kv -> kv.Key, kv.Value)
            |> Map.ofSeq

        let actualPruned =
            subtree
            |> Seq.map (fun kv -> kv.Key, kv.Value)
            |> Map.ofSeq

        let actualRemaining =
            sut
            |> Seq.map (fun kv -> kv.Key, kv.Value)
            |> Map.ofSeq

        actualPruned = expectedPruned && actualRemaining = expectedRemaining

    [<Property>]
    let ``Removing keys while iterating Keys should not throw and should clear the tree`` (keys: Set<NonEmptyString>) =
        let sut = ctor Seq.empty

        for key in keys do
            sut.Add(key.Get, 0)

        try
            for key in sut.Keys do
                sut.Remove(key) |> ignore

            Seq.isEmpty sut.Keys
        with _ ->
            false
