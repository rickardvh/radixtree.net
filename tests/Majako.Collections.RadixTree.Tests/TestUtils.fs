[<AutoOpen>]
module Majako.Collections.RadixTree.Tests.TestUtils

open Swensen.Unquote.Assertions

/// Compares two sequences for equality, ignoring order
let (==!) xs ys = (Seq.sort xs |> Seq.toList) =! (Seq.sort ys |> Seq.toList)

let (==) xs ys =
    let xs' = Seq.sort xs
    let ys' = Seq.sort ys
    Seq.forall2 (fun e a -> e = a) xs' ys'
