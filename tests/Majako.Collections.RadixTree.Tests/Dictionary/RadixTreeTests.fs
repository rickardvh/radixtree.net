namespace Majako.Collections.RadixTree.Tests.Dictionary

open Majako.Collections.RadixTree

type RadixTreeTests() =
    inherit PrefixTreeTestBase(fun () -> RadixTree<int>())
