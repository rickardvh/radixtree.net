namespace Majako.Collections.RadixTree.Tests.Dictionary

open Majako.Collections.RadixTree.Concurrent

type ConcurrentTriePropertyTests() =
    inherit PrefixTreePropertyTestBase(fun items -> ConcurrentTrie<int>(items))
