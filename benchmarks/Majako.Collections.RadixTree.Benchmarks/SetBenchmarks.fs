namespace Majako.Collections.RadixTree.Benchmarks

open System
open System.Collections.Generic
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Majako.Collections.RadixTree

[<MemoryDiagnoser>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type SetBenchmarks() =
    [<DefaultValue>]
    val mutable private keys: string array
    [<DefaultValue>]
    val mutable private probes: string array
    [<DefaultValue>]
    val mutable private prefix: string
    [<DefaultValue>]
    val mutable private hashSet: HashSet<string>
    [<DefaultValue>]
    val mutable private treeSet: RadixTree

    [<Params(10000, 50000)>]
    member val ItemCount = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        this.keys <- BenchmarkData.generateKeys this.ItemCount
        this.probes <- BenchmarkData.sample this.keys 1024
        this.prefix <- BenchmarkData.pickPrefix this.keys
        this.hashSet <- HashSet<string>(this.keys)
        this.treeSet <- RadixTree(this.keys)

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("Add")>]
    member this.HashSetAdd() =
        let set = HashSet<string>()

        for key in this.keys do
            set.Add(key) |> ignore

        set.Count

    [<Benchmark>]
    [<BenchmarkCategory("Add")>]
    member this.RadixSetAdd() =
        let set = RadixTree()

        for key in this.keys do
            set.Add(key) |> ignore

        set.Count

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("Contains")>]
    member this.HashSetContains() =
        let mutable count = 0

        for key in this.probes do
            if this.hashSet.Contains(key) then
                count <- count + 1

        count

    [<Benchmark>]
    [<BenchmarkCategory("Contains")>]
    member this.RadixSetContains() =
        let mutable count = 0

        for key in this.probes do
            if this.treeSet.Contains(key) then
                count <- count + 1

        count

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("PrefixSearch")>]
    member this.HashSetPrefixScan() =
        let mutable count = 0

        for key in this.hashSet do
            if key.StartsWith(this.prefix, StringComparison.Ordinal) then
                count <- count + 1

        count

    [<Benchmark>]
    [<BenchmarkCategory("PrefixSearch")>]
    member this.RadixSetPrefixSearch() =
        let mutable count = 0

        for _ in this.treeSet.Search(this.prefix) do
            count <- count + 1

        count

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("Remove")>]
    member this.HashSetRemoveAll() =
        let set = HashSet<string>(this.keys)

        for key in this.keys do
            set.Remove(key) |> ignore

        set.Count

    [<Benchmark>]
    [<BenchmarkCategory("Remove")>]
    member this.RadixSetRemoveAll() =
        let set = RadixTree(this.keys)

        for key in this.keys do
            set.Remove(key) |> ignore

        set.Count
