[<AutoOpen>]
module Majako.Collections.RadixTree.Tests.TestUtils

open Swensen.Unquote.Assertions

/// Compares two sequences for equality, ignoring order
let (==!) a b = (Seq.toList a |> List.sort) =! (Seq.toList b |> List.sort)
