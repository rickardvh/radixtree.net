namespace Majako.Collections.RadixTree.Tests.Set

open Majako.Collections.RadixTree

type RadixTreeTests() =
    inherit PrefixTreeTestBase(fun items -> if items.IsSome then RadixTree items.Value else RadixTree())
