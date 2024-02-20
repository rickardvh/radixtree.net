module Majako.Collections.RadixTree.Tests.ConcurrentTriePropertyTests

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree
open System.Collections.Generic

type TestItems = list<NonEmptyString * int>

let makeTrie (items: TestItems) =
    ConcurrentTrie<int>(items |> List.map (fun (k, v) -> KeyValuePair(k.Get, v)))

[<Property>]
let ``A key/value pair should exist after being added`` (items: TestItems, key: NonEmptyString, value: int) =
    let sut = makeTrie items
    sut.Add(key.Get, value)
    let found, actual = sut.TryGetValue(key.Get)
    found && actual = value

[<Property>]
let ``A key/value pair should not exist after being added and removed`` (items: TestItems, key: NonEmptyString, value: int) =
    let sut = makeTrie items
    sut.Add(key.Get, value)
    sut.Remove(key.Get)
    let found, _ = sut.TryGetValue(key.Get)
    not found

[<Property>]
let ``A key/value pair should not exist after being removed`` (items: TestItems, key: NonEmptyString) =
    let sut = makeTrie items
    sut.Remove(key.Get)
    let found, _ = sut.TryGetValue(key.Get)
    not found

[<Property>]
let ``A key/value pair should exist after being added and removed and added again`` (items: TestItems, key: NonEmptyString, value: int) =
    let sut = makeTrie items
    sut.Add(key.Get, value)
    sut.Remove(key.Get)
    sut.Add(key.Get, value)
    let found, actual = sut.TryGetValue(key.Get)
    found && actual = value

[<Property>]
let ``A key/value pair should exist after being added and removed and added again with a different value`` (items: TestItems, key: NonEmptyString, value: int, value2: int) =
    let sut = makeTrie items
    sut.Add(key.Get, value)
    sut.Remove(key.Get)
    sut.Add(key.Get, value2)
    let found, actual = sut.TryGetValue(key.Get)
    found && actual = value2

[<Property>]
let ``Keys should be the same as the ones added`` (keys: Set<NonEmptyString>) =
    let sut = ConcurrentTrie<int>()
    for key in keys do
        sut.Add(key.Get, 0)
    let expected = keys |> Seq.map (fun k -> k.Get)
    sut.Keys == expected

[<Property>]
let ``A pruned subtree should contain only keys with the given prefix`` (items: TestItems, prefix: string) =
    let sut = makeTrie items
    let subTree  = sut.Prune(prefix)
    subTree.Keys |> Seq.forall (fun k -> k.StartsWith prefix)

[<Property>]
let ``Pruning should remove all keys with the given prefix`` (items: TestItems, prefix: UnicodeString) =
    // note: "a".StartsWith "\u0017" mistakenly evaluates to true, so we use a UnicodeString here
    let sut = makeTrie items
    sut.Prune(prefix.Get) |> ignore
    sut.Keys |> Seq.forall (fun k -> not (k.StartsWith prefix.Get))

[<Property>]
let ``Clearing the tree should remove all keys`` (items: TestItems) =
    let sut = makeTrie items
    sut.Clear()
    Seq.isEmpty sut.Keys
