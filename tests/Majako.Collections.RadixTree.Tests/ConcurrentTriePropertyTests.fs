module ConcurrentTrieTests

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree

[<Property>]
let ``Prune with existing prefix should return true and sub-collection`` (prefix: string) =
    let trie = ConcurrentTrie<string>()
    let value = "value"
    let value2 = "value2"
    trie.Add(prefix, value)
    trie.Add(prefix + "ing", value2)

    let result = trie.Prune(prefix, out var subCollection)
    result = true && subCollection.ContainsKey(prefix)

[<Property>]
let ``Prune with non-existing prefix should return false and default sub-collection`` (prefix: string) =
    let trie = ConcurrentTrie<string>()
    let value = "value"
    trie.Add("test", value)

    let result = trie.Prune(prefix, out var subCollection)
    result = false && subCollection = null

[<Property>]
let ``Prune with empty prefix should return true and all collection`` () =
    let trie = ConcurrentTrie<string>()
    let prefix = "test"
    let value = "value"
    let value2 = "value2"
    trie.Add(prefix, value)
    trie.Add(prefix + "ing", value2)

    let result = trie.Prune("", out var subCollection)
    result = true && subCollection.ContainsKey(prefix) && subCollection.ContainsKey(prefix + "ing")
