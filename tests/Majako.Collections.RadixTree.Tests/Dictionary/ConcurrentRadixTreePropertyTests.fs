namespace Majako.Collections.RadixTree.Tests.Dictionary

open Majako.Collections.RadixTree.Concurrent

type ConcurrentRadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentRadixTree<int>(items))
