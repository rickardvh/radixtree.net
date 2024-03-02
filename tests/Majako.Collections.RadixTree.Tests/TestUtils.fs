[<AutoOpen>]
module Majako.Collections.RadixTree.Tests.TestUtils

open Swensen.Unquote.Assertions

/// Assertion comparing two sequences for equality, ignoring order
let (==!) xs ys = (Seq.sort xs |> Seq.toList) =! (Seq.sort ys |> Seq.toList)

let (==) xs ys =
    let xs' = Seq.sort xs |> Seq.toList
    let ys' = Seq.sort ys |> Seq.toList
    xs' = ys'

/// Generalised startsWith function for sequences; motivated by a bug where string.StartsWith trims
/// any control characters from both strings, which causes FsCheck tests to fail when they shouldn't
let startsWith (xs: seq<'a>) (prefix: seq<'a>) =
    use xsEnumerator = xs.GetEnumerator()
    use prefixEnumerator = prefix.GetEnumerator()
    let mutable state = true
    while (state && prefixEnumerator.MoveNext()) do
        state <- xsEnumerator.MoveNext() && xsEnumerator.Current = prefixEnumerator.Current
    state
