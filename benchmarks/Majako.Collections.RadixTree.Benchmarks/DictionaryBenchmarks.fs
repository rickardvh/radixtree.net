namespace Majako.Collections.RadixTree.Benchmarks

open System
open System.Collections.Generic
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Majako.Collections.RadixTree

[<MemoryDiagnoser>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type DictionaryBenchmarks() =
    [<DefaultValue>]
    val mutable private keys: string array
    [<DefaultValue>]
    val mutable private probes: string array
    [<DefaultValue>]
    val mutable private prefix: string
    [<DefaultValue>]
    val mutable private dict: Dictionary<string, int>
    [<DefaultValue>]
    val mutable private tree: RadixTree<int>

    [<Params(10000, 50000)>]
    member val ItemCount = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        this.keys <- BenchmarkData.generateKeys this.ItemCount
        this.probes <- BenchmarkData.sample this.keys 1024
        this.prefix <- BenchmarkData.pickPrefix this.keys
        this.dict <- Dictionary<string, int>(this.keys.Length)
        this.tree <- RadixTree<int>()

        for i = 0 to this.keys.Length - 1 do
            let key = this.keys[i]
            this.dict[key] <- i
            this.tree[key] <- i

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("Add")>]
    member this.DictionaryAdd() =
        let dict = Dictionary<string, int>(this.keys.Length)

        for i = 0 to this.keys.Length - 1 do
            dict[this.keys[i]] <- i

        dict.Count

    [<Benchmark>]
    [<BenchmarkCategory("Add")>]
    member this.RadixTreeAdd() =
        let tree = RadixTree<int>()

        for i = 0 to this.keys.Length - 1 do
            tree[this.keys[i]] <- i

        tree.Count

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("Lookup")>]
    member this.DictionaryTryGetValue() =
        let mutable sum = 0

        for key in this.probes do
            let found, value = this.dict.TryGetValue(key)
            if found then
                sum <- sum + value

        sum

    [<Benchmark>]
    [<BenchmarkCategory("Lookup")>]
    member this.RadixTreeTryGetValue() =
        let mutable sum = 0

        for key in this.probes do
            let found, value = this.tree.TryGetValue(key)
            if found then
                sum <- sum + value

        sum

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("PrefixSearch")>]
    member this.DictionaryPrefixScan() =
        let mutable count = 0

        for kv in this.dict do
            if kv.Key.StartsWith(this.prefix, StringComparison.Ordinal) then
                count <- count + 1

        count

    [<Benchmark>]
    [<BenchmarkCategory("PrefixSearch")>]
    member this.RadixTreePrefixSearch() =
        let mutable count = 0

        for _ in this.tree.Search(this.prefix) do
            count <- count + 1

        count

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("Remove")>]
    member this.DictionaryRemoveAll() =
        let dict = Dictionary<string, int>(this.keys.Length)

        for i = 0 to this.keys.Length - 1 do
            dict[this.keys[i]] <- i

        for key in this.keys do
            dict.Remove(key) |> ignore

        dict.Count

    [<Benchmark>]
    [<BenchmarkCategory("Remove")>]
    member this.RadixTreeRemoveAll() =
        let tree = RadixTree<int>()

        for i = 0 to this.keys.Length - 1 do
            tree[this.keys[i]] <- i

        for key in this.keys do
            tree.Remove(key) |> ignore

        tree.Count
