namespace Majako.Collections.RadixTree.Tests.Set

open Xunit
open Swensen.Unquote
open System
open Majako.Collections.RadixTree
open Majako.Collections.RadixTree.Tests


[<AbstractClass>]
type PrefixTreeTestBase(ctor: option<string seq> -> IPrefixTree) =
    [<Fact>]
    let ``Can add item`` () =
        let sut = ctor None
        test <@ not <| sut.Contains "a" @>
        let added = sut.Add "a"
        test <@ added @>
        test <@ sut.Contains "a" @>

    [<Fact>]
    let ``Can add and get items`` () =
        let sut = ctor None
        sut.Add "a" |> ignore
        test <@ not <| sut.Contains "ab" @>
        sut.Add "abc" |> ignore
        test <@ not <| sut.Contains "ab" @>
        test <@ sut.Contains "a" @>
        test <@ sut.Contains "abc" @>
        sut.Add "ab" |> ignore
        test <@ sut.Contains "ab" @>

    [<Fact>]
    let ``Throws when trying to add null or empty item`` () =
        let sut = ctor None
        raises<ArgumentException> <@ sut.Add "" @>
        raises<ArgumentException> <@ sut.Add null @>

    [<Fact>]
    let ``Throws when trying to remove null or empty item`` () =
        let sut = ctor None
        raises<ArgumentException> <@ sut.Remove "" @>
        raises<ArgumentException> <@ sut.Remove null @>

    [<Fact>]
    let ``Can remove item`` () =
        let sut = ctor <| Some [ "abc"; "a"; "b"; "aa"; "abb"; "bbb"; "ab" ]

        sut.Remove "ab" |> ignore
        test <@ not <| sut.Contains "ab" @>
        sut ==! [ "abc"; "a"; "b"; "aa"; "abb"; "bbb" ]
        sut.Remove "ab" |> ignore
        sut.Remove "bb" |> ignore
        test <@ sut.Contains "b" @>
        test <@ sut.Contains "bbb" @>

        let subtree = sut.Prune("b")
        subtree ==! [ "b"; "bbb" ]
        subtree.Remove "b" |> ignore
        subtree ==! [ "bbb" ]

    [<Fact>]
    let ``Can prune`` () =
        let sut = ctor <| Some [ "a"; "b"; "bba"; "bbb"; "ab"; "aa"; "abc"; "abb" ]

        let subtree = sut.Prune "ab"
        subtree ==! [ "ab"; "abc"; "abb" ]
        sut ==! [ "a"; "b"; "aa"; "bba"; "bbb" ]

    [<Fact>]
    let ``Can prune all`` () =
        let sut = ctor <| Some [ "a"; "b"; "bba"; "bbb"; "ab"; "aa"; "abc"; "abb" ]

        let subtree = sut.Prune ""
        subtree ==! [ "a"; "b"; "ab"; "aa"; "abc"; "abb"; "bba"; "bbb" ]
        sut ==! []

    [<Fact>]
    let ``Can prune none`` () =
        let sut = ctor <| Some [ "a"; "b"; "bba"; "bbb"; "ab"; "aa"; "abc"; "abb" ]

        let subtree = sut.Prune "c"
        subtree ==! []
        sut ==! [ "a"; "b"; "ab"; "aa"; "abc"; "abb"; "bba"; "bbb" ]

    [<Fact>]
    let ``Can search`` () =
        let sut = ctor <| Some [ "a"; "b"; "bba"; "bbb"; "ab"; "aa"; "abc"; "abb" ]

        sut.Search("ab") ==! [ "ab"; "abc"; "abb" ]
        sut.Search("b") ==! [ "b"; "bba"; "bbb" ]
        sut.Search("c") ==! []

    [<Fact>]
    let ``Can clear`` () =
        let sut = ctor <| Some [ "a"; "b"; "bba"; "bbb"; "ab"; "aa"; "abc"; "abb" ]

        sut.Clear()
        sut ==! []
