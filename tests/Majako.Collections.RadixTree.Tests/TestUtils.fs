[<AutoOpen>]
module Majako.Collections.RadixTree.Tests.TestUtils

open Swensen.Unquote.Assertions

/// Assertion comparing two sequences for equality, ignoring order
let inline (==!) xs ys = (Seq.sort xs |> Seq.toList) =! (Seq.sort ys |> Seq.toList)

let rec eq xs ys =
    match xs, ys with
    | [], [] -> true
    | _::_, [] -> false
    | [], _::_ -> false
    | x::xs', y::ys' -> x = y && eq xs' ys'

let inline (==) xs ys =
    let xs' = Seq.sort xs |> Seq.toList
    let ys' = Seq.sort ys |> Seq.toList
    eq xs' ys'

/// Generalised startsWith function for sequences; motivated by a bug where string.StartsWith trims
/// any control characters from both strings, which causes FsCheck tests to fail when they shouldn't
let startsWith (xs: seq<'a>) (prefix: seq<'a>) =
    use xsEnumerator = xs.GetEnumerator()
    use prefixEnumerator = prefix.GetEnumerator()
    let mutable state = true

    while (state && prefixEnumerator.MoveNext()) do
        state <- xsEnumerator.MoveNext() && xsEnumerator.Current = prefixEnumerator.Current

    state
