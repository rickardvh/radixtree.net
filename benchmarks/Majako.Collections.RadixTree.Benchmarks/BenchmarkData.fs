namespace Majako.Collections.RadixTree.Benchmarks

open System

module BenchmarkData =
    let generateKeys (count: int) =
        Array.init count (fun i -> $"k{i:D8}-{i % 257:D3}")

    let pickPrefix (keys: string array) =
        if keys.Length = 0 then
            String.Empty
        else
            let key = keys[keys.Length / 2]
            key.Substring(0, Math.Min(4, key.Length))

    let sample (keys: string array) (maxSamples: int) =
        if keys.Length = 0 then
            [||]
        else
            let step = Math.Max(1, keys.Length / Math.Max(1, maxSamples))
            [| for i in 0 .. step .. keys.Length - 1 -> keys[i] |]

    let buildWorkerBatches (threadCount: int) (itemsPerWorker: int) (contentionPercent: int) =
        let shared = itemsPerWorker * contentionPercent / 100
        Array.init threadCount (fun worker ->
            Array.init itemsPerWorker (fun i ->
                if i < shared then
                    $"shared-{i:D8}"
                else
                    $"w{worker:D2}-k{i:D8}"))
