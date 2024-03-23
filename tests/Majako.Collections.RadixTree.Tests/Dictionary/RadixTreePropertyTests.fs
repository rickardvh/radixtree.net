namespace Majako.Collections.RadixTree.Tests.Dictionary

open Majako.Collections.RadixTree

type RadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> RadixTree<int>(items))
