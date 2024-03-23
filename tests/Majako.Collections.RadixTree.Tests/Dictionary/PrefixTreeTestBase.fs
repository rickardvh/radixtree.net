namespace Majako.Collections.RadixTree.Tests.Dictionary

open Xunit
open Swensen.Unquote
open System
open Majako.Collections.RadixTree
open Majako.Collections.RadixTree.Tests

[<AbstractClass>]
type PrefixTreeTestBase(ctor: unit -> IPrefixTree<int>) =
    [<Fact>]
    let ``Can add and get value`` () =
        let sut = ctor ()
        let found, _ = sut.TryGetValue "a"
        test <@ not found @>
        sut.Add("a", 1)
        let found, value = sut.TryGetValue "a"
        test <@ found @>
        value =! 1
        sut.Add("a", 2)
        let found, value = sut.TryGetValue "a"
        test <@ found @>
        value =! 2

    [<Fact>]
    let ``Can add and get values`` () =
        let sut = ctor ()
        sut.Add("a", 1)
        let found, _ = sut.TryGetValue "ab"
        test <@ not found @>
        sut.Add("abc", 3)
        let found, _ = sut.TryGetValue "ab"
        test <@ not found @>
        let found, value = sut.TryGetValue "a"
        test <@ found @>
        value =! 1
        let found, value = sut.TryGetValue "abc"
        test <@ found @>
        value =! 3
        sut.Add("ab", 2)
        let found, value = sut.TryGetValue "ab"
        test <@ found @>
        value =! 2

    [<Fact>]
    let ``Does not block while enumerating`` () =
        let sut = ctor ()
        sut.Add("a", 0)
        sut.Add("ab", 0)

        for item in sut.Keys do
            sut.Remove item |> ignore

    [<Fact>]
    let ``Throws when trying to add null or empty key`` () =
        let sut = ctor ()
        raises<ArgumentException> <@ sut.Add("", 0) @>
        raises<ArgumentException> <@ sut.Add(null, 0) @>

    [<Fact>]
    let ``Throws when trying to remove null or empty key`` () =
        let sut = ctor ()
        raises<ArgumentException> <@ sut.Remove "" @>
        raises<ArgumentException> <@ sut.Remove null @>

    [<Fact>]
    let ``Can remove value`` () =
        let sut = ctor ()
        sut.Add("a", 1)
        sut.Add("b", 1)
        sut.Add("bbb", 1)
        sut.Add("ab", 1)
        sut.Add("aa", 1)
        sut.Add("abc", 1)
        sut.Add("abb", 1)
        sut.Remove "ab" |> ignore
        let found, _ = sut.TryGetValue "ab"
        test <@ not found @>
        sut.Keys ==! [ "abc"; "a"; "b"; "aa"; "abb"; "bbb" ]
        sut.Remove "ab" |> ignore
        sut.Remove "bb" |> ignore
        let found, _ = sut.TryGetValue "b"
        test <@ found @>
        let found, _ = sut.TryGetValue "bbb"
        test <@ found @>

        let subtree = sut.Prune "b"
        subtree.Keys ==! [ "b"; "bbb" ]
        subtree.Remove "b" |> ignore
        subtree.Keys ==! [ "bbb" ]

    [<Fact>]
    let ``Can get keys`` () =
        let sut = ctor ()
        let keys = [ "a"; "b"; "abc" ]

        for key in keys do
            sut.Add(key, 1)

        sut.Keys ==! keys

    [<Fact>]
    let ``Can prune`` () =
        let sut = ctor ()
        sut.Add("a", 1)
        sut.Add("b", 1)
        sut.Add("bba", 1)
        sut.Add("bbb", 1)
        sut.Add("ab", 1)
        sut.Add("aa", 1)
        sut.Add("abc", 1)
        sut.Add("abb", 1)
        let subtree = sut.Prune "ab"
        subtree.Keys ==! [ "ab"; "abc"; "abb" ]
        sut.Keys ==! [ "a"; "b"; "aa"; "bba"; "bbb" ]

    [<Fact>]
    let ``Can prune all`` () =
        let sut = ctor ()
        sut.Add("a", 1)
        sut.Add("b", 1)
        sut.Add("bba", 1)
        sut.Add("bbb", 1)
        sut.Add("ab", 1)
        sut.Add("aa", 1)
        sut.Add("abc", 1)
        sut.Add("abb", 1)
        let subtree = sut.Prune ""
        subtree.Keys ==! [ "a"; "b"; "ab"; "aa"; "abc"; "abb"; "bba"; "bbb" ]
        sut.Keys ==! []

    [<Fact>]
    let ``Can prune none`` () =
        let sut = ctor ()
        sut.Add("a", 1)
        sut.Add("b", 1)
        sut.Add("bba", 1)
        sut.Add("bbb", 1)
        sut.Add("ab", 1)
        sut.Add("aa", 1)
        sut.Add("abc", 1)
        sut.Add("abb", 1)
        let subtree = sut.Prune "c"
        subtree.Keys ==! []
        sut.Keys ==! [ "a"; "b"; "ab"; "aa"; "abc"; "abb"; "bba"; "bbb" ]

    [<Fact>]
    let ``Can search`` () =
        let sut = ctor ()
        sut.Add("a", 1)
        sut.Add("b", 1)
        sut.Add("bba", 1)
        sut.Add("bbb", 1)
        sut.Add("ab", 1)
        sut.Add("aa", 1)
        sut.Add("abc", 1)
        sut.Add("abb", 1)
        sut.Search "ab" |> Seq.map (fun kv -> kv.Key) ==! [ "ab"; "abc"; "abb" ]
        sut.Search "b" |> Seq.map (fun kv -> kv.Key) ==! [ "b"; "bba"; "bbb" ]
        sut.Search "c" |> Seq.map (fun kv -> kv.Key) ==! []

    [<Fact>]
    let ``Can clear`` () =
        let sut = ctor ()
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
