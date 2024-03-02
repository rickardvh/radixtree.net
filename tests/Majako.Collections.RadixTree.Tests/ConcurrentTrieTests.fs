module Majako.Collections.RadixTree.Tests.ConcurrentTrieTests

open Xunit
open Swensen.Unquote
open Majako.Collections.RadixTree

[<Fact>]
let ``Can add and get value`` () =
    let sut = ConcurrentTrie<int>()
    let found, _ = sut.TryGetValue("a")
    test <@ not found @>
    sut.Add("a", 1)
    let found, value = sut.TryGetValue("a")
    test <@ found @>
    value =! 1
    sut.Add("a", 2)
    let found, value = sut.TryGetValue("a")
    test <@ found @>
    value =! 2

[<Fact>]
let ``Can add and get values`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    let found, _ = sut.TryGetValue("ab")
    test <@ not found @>
    sut.Add("abc", 3)
    let found, _ = sut.TryGetValue("ab")
    test <@ not found @>
    let found, value = sut.TryGetValue("a")
    test <@ found @>
    value =! 1
    let found, value = sut.TryGetValue("abc")
    test <@ found @>
    value =! 3
    sut.Add("ab", 2)
    let found, value = sut.TryGetValue("ab")
    test <@ found @>
    value =! 2

[<Fact>]
let ``Does not block while enumerating`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 0)
    sut.Add("ab", 0)

    for item in sut.Keys do
        sut.Remove(item) |> ignore

[<Fact>]
let ``Can remove value`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    sut.Add("b", 1)
    sut.Add("bbb", 1)
    sut.Add("ab", 1)
    sut.Add("aa", 1)
    sut.Add("abc", 1)
    sut.Add("abb", 1)
    sut.Remove("ab")
    let found, _ = sut.TryGetValue("ab")
    test <@ not found @>
    sut.Keys ==! [ "abc"; "a"; "b"; "aa"; "abb"; "bbb" ]
    sut.Remove("ab")
    sut.Remove("bb")
    let found, _ = sut.TryGetValue("b")
    test <@ found @>
    let found, _ = sut.TryGetValue("bbb")
    test <@ found @>

    let sut = sut.Prune("b")
    sut.Keys ==! [ "b"; "bbb" ]
    sut.Remove("b")
    sut.Keys ==! [ "bbb" ]

[<Fact>]
let ``Can get keys`` () =
    let sut = ConcurrentTrie<int>()
    let keys = [ "a"; "b"; "abc" ]

    for key in keys do
        sut.Add(key, 1)

    sut.Keys ==! keys

[<Fact>]
let ``Can prune`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    sut.Add("b", 1)
    sut.Add("bba", 1)
    sut.Add("bbb", 1)
    sut.Add("ab", 1)
    sut.Add("aa", 1)
    sut.Add("abc", 1)
    sut.Add("abb", 1)
    let subtree = sut.Prune("ab")
    subtree.Keys ==! [ "ab"; "abc"; "abb" ]
    sut.Keys ==! [ "a"; "b"; "aa"; "bba"; "bbb" ]

[<Fact>]
let ``Can prune all`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    sut.Add("b", 1)
    sut.Add("bba", 1)
    sut.Add("bbb", 1)
    sut.Add("ab", 1)
    sut.Add("aa", 1)
    sut.Add("abc", 1)
    sut.Add("abb", 1)
    let subtree = sut.Prune("")
    subtree.Keys ==! [ "a"; "b"; "ab"; "aa"; "abc"; "abb"; "bba"; "bbb" ]
    sut.Keys ==! []

[<Fact>]
let ``Can prune none`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    sut.Add("b", 1)
    sut.Add("bba", 1)
    sut.Add("bbb", 1)
    sut.Add("ab", 1)
    sut.Add("aa", 1)
    sut.Add("abc", 1)
    sut.Add("abb", 1)
    let subtree = sut.Prune("c")
    subtree.Keys ==! []
    sut.Keys ==! [ "a"; "b"; "ab"; "aa"; "abc"; "abb"; "bba"; "bbb" ]

[<Fact>]
let ``Can search`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    sut.Add("b", 1)
    sut.Add("bba", 1)
    sut.Add("bbb", 1)
    sut.Add("ab", 1)
    sut.Add("aa", 1)
    sut.Add("abc", 1)
    sut.Add("abb", 1)
    sut.Search("ab") |> Seq.map (fun kv -> kv.Key) ==! [ "ab"; "abc"; "abb" ]
    sut.Search("b") |> Seq.map (fun kv -> kv.Key) ==! [ "b"; "bba"; "bbb" ]
    sut.Search("c") |> Seq.map (fun kv -> kv.Key) ==! []

[<Fact>]
let ``Can clear`` () =
    let sut = ConcurrentTrie<int>()
    sut.Add("a", 1)
    sut.Add("b", 1)
    sut.Add("bba", 1)
    sut.Add("bbb", 1)
    sut.Add("ab", 1)
    sut.Add("aa", 1)
    sut.Add("abc", 1)
    sut.Add("abb", 1)
    sut.Clear()
    sut.Keys ==! []

[<Fact>]
let ``Does not break during parallel add remove`` () =
    let sut = ConcurrentTrie<int>()

    let addRemove j =
        for i in 1..1000 do
            let key = $"{i}-{j}"
            sut.Add(key, i)
            let found, value = sut.TryGetValue(key)
            test <@ found @>
            value =! i
            sut.Remove(key)
            let found, _ = sut.TryGetValue(key)
            test <@ not found @>

    Array.Parallel.iter addRemove [| 1..1000 |]

    test <@ Seq.isEmpty sut.Keys @>

[<Fact>]
let ``Does not break during parallel add prune`` () =
    let sut = ConcurrentTrie<int>()

    let addPrune j =
        for i in 1..1000 do
            sut.Add($"{j}-{i}", i)

        let subtree = sut.Prune($"{j}-")
        Seq.length subtree.Keys =! 1000

    Array.Parallel.iter addPrune [| 1..1000 |]

    test <@ Seq.isEmpty sut.Keys @>
