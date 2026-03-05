namespace Majako.Collections.RadixTree.Benchmarks

open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open Majako.Collections.RadixTree.Concurrent

[<MemoryDiagnoser>]
[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
type ConcurrentBenchmarks() =
    [<DefaultValue>]
    val mutable private workerBatches: string array array
    [<DefaultValue>]
    val mutable private probeBatches: string array array
    [<DefaultValue>]
    val mutable private dictionaryForReads: ConcurrentDictionary<string, int>
    [<DefaultValue>]
    val mutable private treeForReads: ConcurrentRadixTree<int>

    [<Params(5000)>]
    member val ItemsPerWorker = 0 with get, set

    [<Params(1, 2, 4, 8)>]
    member val ThreadCount = 0 with get, set

    [<Params(0, 50)>]
    member val ContentionPercent = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        this.workerBatches <- BenchmarkData.buildWorkerBatches this.ThreadCount this.ItemsPerWorker this.ContentionPercent
        this.probeBatches <- this.workerBatches |> Array.map (fun keys -> BenchmarkData.sample keys 256)
        this.dictionaryForReads <- ConcurrentDictionary<string, int>()
        this.treeForReads <- ConcurrentRadixTree<int>()

        for worker = 0 to this.workerBatches.Length - 1 do
            for key in this.workerBatches[worker] do
                this.dictionaryForReads[key] <- worker
                this.treeForReads[key] <- worker

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("AddParallel")>]
    member this.ConcurrentDictionaryAddParallel() =
        let dict = ConcurrentDictionary<string, int>()
        let options = ParallelOptions(MaxDegreeOfParallelism = this.ThreadCount)

        Parallel.For(0, this.ThreadCount, options, fun worker ->
            for key in this.workerBatches[worker] do
                dict[key] <- worker) |> ignore

        dict.Count

    [<Benchmark>]
    [<BenchmarkCategory("AddParallel")>]
    member this.ConcurrentRadixTreeAddParallel() =
        let tree = ConcurrentRadixTree<int>()
        let options = ParallelOptions(MaxDegreeOfParallelism = this.ThreadCount)

        Parallel.For(0, this.ThreadCount, options, fun worker ->
            for key in this.workerBatches[worker] do
                tree[key] <- worker) |> ignore

        tree.Count

    [<Benchmark(Baseline = true)>]
    [<BenchmarkCategory("LookupParallel")>]
    member this.ConcurrentDictionaryTryGetParallel() =
        let mutable total = 0
        let options = ParallelOptions(MaxDegreeOfParallelism = this.ThreadCount)

        Parallel.For(0, this.ThreadCount, options, fun worker ->
            let mutable local = 0

            for key in this.probeBatches[worker] do
                let found, value = this.dictionaryForReads.TryGetValue(key)
                if found then
                    local <- local + value

            Interlocked.Add(&total, local) |> ignore) |> ignore

        total

    [<Benchmark>]
    [<BenchmarkCategory("LookupParallel")>]
    member this.ConcurrentRadixTreeTryGetParallel() =
        let mutable total = 0
        let options = ParallelOptions(MaxDegreeOfParallelism = this.ThreadCount)

        Parallel.For(0, this.ThreadCount, options, fun worker ->
            let mutable local = 0

            for key in this.probeBatches[worker] do
                let found, value = this.treeForReads.TryGetValue(key)
                if found then
                    local <- local + value

            Interlocked.Add(&total, local) |> ignore) |> ignore

        total
