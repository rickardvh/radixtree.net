module ConcurrentTrieTests

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree

[<Property>]
let ``A key/value pair should exist after being added`` (sut: ConcurrentTrie<int>, key: NonEmptyString, value: int) =
    sut.Add(key.Get, value)
    let (found, actual) = sut.TryGetValue(key.Get)
    found && actual = value

[<Property>]
let ``A key/value pair should not exist after being added and removed`` (sut: ConcurrentTrie<int>, key: NonEmptyString, value: int) =
    sut.Add(key.Get, value)
    sut.Remove(key.Get)
    let (found, _) = sut.TryGetValue(key.Get)
    not found

[<Property>]
let ``A key/value pair should not exist after being removed`` (sut: ConcurrentTrie<int>, key: NonEmptyString) =
    sut.Remove(key.Get)
    let (found, _) = sut.TryGetValue(key.Get)
    not found

[<Property>]
let ``A key/value pair should exist after being added and removed and added again`` (sut: ConcurrentTrie<int>, key: NonEmptyString, value: int) =
    sut.Add(key.Get, value)
    sut.Remove(key.Get)
    sut.Add(key.Get, value)
    let (found, actual) = sut.TryGetValue(key.Get)
    found && actual = value

[<Property>]
let ``A key/value pair should exist after being added and removed and added again with a different value`` (sut: ConcurrentTrie<int>, key: NonEmptyString, value: int, value2: int) =
    sut.Add(key.Get, value)
    sut.Remove(key.Get)
    sut.Add(key.Get, value2)
    let (found, actual) = sut.TryGetValue(key.Get)
    found && actual = value2

[<Property>]
let ``Keys should be the same as the ones added`` (keys: Set<NonEmptyString>) =
    let sut = ConcurrentTrie<int>()
    for key in keys do
        sut.Add(key.Get, 0)
    let expected = keys |> Seq.map (fun k -> k.Get) |> Seq.sort
    let actual = sut.Keys |> Seq.sort
    Seq.forall2 (fun e a -> e = a) expected actual
