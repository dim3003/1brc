using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;

namespace Benchmarks.Level03_Parallel;

/// <summary>
/// Benchmark: Thread-Local vs Shared Dictionary
/// 
/// Tests: Lock-free thread-local vs ConcurrentDictionary
/// Purpose: Demonstrate why Level 3 uses thread-local pattern
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Level03", "Concurrency")]
public class ThreadLocalVsSharedBenchmark
{
    private List<(string Station, double Temp)> _measurements = [];
    private readonly object _lock = new();

    [Params(100_000, 1_000_000, 10_000_000)]
    public int MeasurementCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(0197);

        // Generate 413 unique station names (matching 1BRC challenge)
        var stations = new string[413];
        for (var i = 0; i < 413; i++)
        {
            stations[i] = $"Station_{i:D3}"; // Station_000 to Station_412
        }

        _measurements = [];
        for (var i = 0; i < MeasurementCount; i++)
        {
            var station = stations[random.Next(stations.Length)];
            var temp = random.NextDouble() * 40 - 10;
            _measurements.Add((station, temp));
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ThreadLocal")]
    public int ThreadLocal()
    {
        var threadCount = Environment.ProcessorCount;
        var chunkSize = MeasurementCount / threadCount;
        var locals = new Dictionary<string, (double, double, double, int)>[threadCount];

        Parallel.For(0, threadCount, i =>
        {
            var start = i * chunkSize;
            var end = (i == threadCount - 1) ? MeasurementCount : (i + 1) * chunkSize;
            var local = new Dictionary<string, (double, double, double, int)>();

            for (var j = start; j < end; j++)
            {
                var (station, temp) = _measurements[j];
            
                if (!local.TryGetValue(station, out var stats))
                    stats = (temp, temp, temp, 1);
                else
                    stats = (Math.Min(stats.Item1, temp), Math.Max(stats.Item2, temp), stats.Item3 + temp, stats.Item4 + 1);
            
                local[station] = stats;
            }

            locals[i] = local;
        });

        // Merge
        var final = new Dictionary<string, (double, double, double, int)>();
        foreach (var local in locals)
        {
            if (local == null) continue;
            foreach (var (station, stats) in local)
            {
                if (!final.TryGetValue(station, out var existing))
                    final[station] = stats;
                else
                    final[station] = (
                        Math.Min(existing.Item1, stats.Item1),
                        Math.Max(existing.Item2, stats.Item2),
                        existing.Item3 + stats.Item3,
                        existing.Item4 + stats.Item4
                    );
            }
        }

        return final.Count;
    }

    [Benchmark]
    [BenchmarkCategory("ConcurrentDict")]
    public int ConcurrentDictionary()
    {
        var shared = new ConcurrentDictionary<string, (double, double, double, int)>();
        var threadCount = Environment.ProcessorCount;
        var chunkSize = MeasurementCount / threadCount;

        Parallel.For(0, threadCount, i =>
        {
            var start = i * chunkSize;
            var end = (i == threadCount - 1) ? MeasurementCount : (i + 1) * chunkSize;

            for (var j = start; j < end; j++)
            {
                var (station, temp) = _measurements[j];

                shared.AddOrUpdate(
                    station,
                    (temp, temp, temp, 1),
                    (key, existing) => (
                        Math.Min(existing.Item1, temp),
                        Math.Max(existing.Item2, temp),
                        existing.Item3 + temp,
                        existing.Item4 + 1
                    )
                );
            }
        });

        return shared.Count;
    }

    [Benchmark]
    [BenchmarkCategory("LockedDict")]
    public int LockedDictionary()
    {
        var shared = new Dictionary<string, (double, double, double, int)>();
        var threadCount = Environment.ProcessorCount;
        var chunkSize = MeasurementCount / threadCount;

        Parallel.For(0, threadCount, i =>
        {
            var start = i * chunkSize;
            var end = (i == threadCount - 1) ? MeasurementCount : (i + 1) * chunkSize;

            for (var j = start; j < end; j++)
            {
                var (station, temp) = _measurements[j];

                lock (_lock)
                {
                    if (!shared.TryGetValue(station, out var stats))
                        stats = (temp, temp, temp, 1);
                    else
                        stats = (Math.Min(stats.Item1, temp), Math.Max(stats.Item2, temp), stats.Item3 + temp, stats.Item4 + 1);

                    shared[station] = stats;
                }
            }
        });

        return shared.Count;
    }
}




/*
 
 BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 7950X 4.50GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.3.26207.106
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


| Method               | MeasurementCount | Mean         | Gen0       | Gen1       | Gen2     | Allocated     |
|--------------------- |----------------- |-------------:|-----------:|-----------:|---------:|--------------:|
| ConcurrentDictionary | 100_000          |   1,836.9 us |   968.7500 |   304.6875 |        - |   15714.98 KB |
| LockedDictionary     | 100_000          |   4,015.3 us |          - |          - |        - |      49.79 KB |
| ThreadLocal          | 100_000          |     526.7 us |   103.5156 |    69.3359 |        - |    1329.96 KB |
|                      |                  |              |            |            |          |               |
| ConcurrentDictionary | 1000000          |  18,044.1 us |  9875.0000 |  1500.0000 | 218.7500 |  156383.62 KB |
| LockedDictionary     | 1000000          |  38,220.4 us |          - |          - |        - |      49.79 KB |
| ThreadLocal          | 1000000          |   1,511.9 us |    99.6094 |    78.1250 |        - |       1332 KB |
|                      |                  |              |            |            |          |               |
| ConcurrentDictionary | 10000000         | 154,472.1 us | 96750.0000 | 11250.0000 | 250.0000 | 1562640.75 KB |
| LockedDictionary     | 10000000         | 375,206.0 us |          - |          - |        - |      50.23 KB |
| ThreadLocal          | 10000000         |  11,414.0 us |    78.1250 |    62.5000 |        - |    1332.64 KB |
 
 */