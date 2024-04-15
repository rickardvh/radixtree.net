namespace Majako.Collections.RadixTree.Tests.Set

open Majako.Collections.RadixTree

type RadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> RadixTree(items))
