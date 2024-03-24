namespace Majako.Collections.RadixTree.Tests.Set

open Majako.Collections.RadixTree.Concurrent

type ConcurrentRadixTreePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentRadixTree(items))
