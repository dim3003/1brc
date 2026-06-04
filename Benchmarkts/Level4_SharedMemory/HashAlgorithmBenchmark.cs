using BenchmarkDotNet.Attributes;
using System.Text;

namespace Benchmarks.Level4_SharedMemory;

/* Test Results
 
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 7950X 4.50GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.3.26207.106
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  Job-MEHJPP : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4

IterationCount=5  WarmupCount=1

| Method                      | MeasurementCount | Mean        | Allocated  | Alloc Ratio |
|---------------------------- |----------------- |------------:|-----------:|------------:|
| DotNetStringHash_Dictionary | 100000           |  3,508.1 us |  4865936 B |       1.000 |
| SimpleHash_Dictionary       | 100000           |  1,526.8 us |    15984 B |       0.003 |
| FNV1aHash_Dictionary        | 100000           |  1,674.5 us |    15984 B |       0.003 |
| PureStringHashCode          | 100000           |  2,363.7 us |  4849952 B |       0.997 |
| PureSimpleHash              | 100000           |    726.8 us |          - |       0.000 |
| PureFNV1aHash               | 100000           |    720.3 us |          - |       0.000 |
|                             |                  |             |            |             |
| DotNetStringHash_Dictionary | 1000000          | 32,539.5 us | 48518352 B |       1.000 |
| SimpleHash_Dictionary       | 1000000          | 15,710.1 us |    15984 B |       0.000 |
| FNV1aHash_Dictionary        | 1000000          | 17,327.8 us |    15984 B |       0.000 |
| PureStringHashCode          | 1000000          | 24,841.2 us | 48502368 B |       1.000 |
| PureSimpleHash              | 1000000          |  7,412.1 us |          - |       0.000 |
| PureFNV1aHash               | 1000000          |  6,870.9 us |          - |       0.000 | 

 
 */




/// <summary>
/// Benchmark: Hash Algorithm Quality Impact on Dictionary Performance
/// 
/// Real 1BRC scenario: Dictionary<int, Stats> where int key comes from hashing byte[]
/// 
/// Key comparison:
///   1. String.GetHashCode() on UTF8 string - general purpose, good quality
///   2. Simple Multiplicative (hash*31+b) - fast but poor distribution
///   3. FNV-1a hash - excellent distribution, optimized for byte sequences
/// 
/// Goal: Show how hash distribution quality affects dictionary performance
/// Test: 413 unique stations, millions of lookups/inserts
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
[BenchmarkCategory("Level04", "Hashing")]
public class HashAlgorithmBenchmark
{
    private byte[][] stationBytes = [];
    private int[] measurementIndices = []; // Simulates repeated stations

    [Params(100_000, 1_000_000)]
    public int MeasurementCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const int stationCount = 413; // Real 1BRC station count

        // Generate 413 realistic station names as byte arrays
        stationBytes = new byte[stationCount][];

        var random = new Random(42);
        var prefixes = new[] { "New", "San", "Port", "Mount", "Saint", "North", "South", "East", "West" };
        var suffixes = new[] { "ville", "town", "city", "burg", "field", "wood", "dale", "mont", "berg" };

        for (var i = 0; i < stationCount; i++)
        {
            string name;
            if (i < 50)
            {
                name = i switch
                {
                    0 => "Hamburg",
                    1 => "Bulawayo",
                    2 => "Palembang",
                    3 => "St. John's",
                    4 => "Cracow",
                    5 => "Bridgetown",
                    6 => "Istanbul",
                    7 => "Roseau",
                    8 => "Conakry",
                    9 => "Bangkok",
                    10 => "Aarhus",
                    11 => "Tashkent",
                    12 => "Amsterdam",
                    13 => "Copenhagen",
                    14 => "Helsinki",
                    _ => $"{prefixes[i % prefixes.Length]}{suffixes[i % suffixes.Length]}_{i}"
                };
            }
            else
            {
                name = $"{prefixes[random.Next(prefixes.Length)]}{suffixes[random.Next(suffixes.Length)]}_{i}";
            }

            stationBytes[i] = Encoding.UTF8.GetBytes(name);
        }

        // Simulate 1BRC: Same stations repeated many times
        measurementIndices = new int[MeasurementCount];
        for (var i = 0; i < MeasurementCount; i++)
        {
            measurementIndices[i] = random.Next(stationCount);
        }
    }

    /// <summary>
    /// Baseline: Using String.GetHashCode() as hash algorithm
    /// Convert byte[] to string, then hash the string
    /// Good distribution but requires string allocation
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("HashAlgorithm")]
    public int DotNetStringHash_Dictionary()
    {
        var dict = new Dictionary<int, int>();

        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            // Simulate: Convert bytes to string, then hash
            var hash = Encoding.UTF8.GetString(bytes).GetHashCode();

            if (dict.TryGetValue(hash, out var count))
                dict[hash] = count + 1;
            else
                dict[hash] = 1;
        }

        return dict.Count;
    }

    /// <summary>
    /// Simple multiplicative hash: hash = hash * 31 + byte
    /// Very fast but poor distribution = more collisions
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("HashAlgorithm")]
    public int SimpleHash_Dictionary()
    {
        var dict = new Dictionary<int, int>();

        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = ComputeSimpleHash(bytes);

            if (dict.TryGetValue(hash, out var count))
                dict[hash] = count + 1;
            else
                dict[hash] = 1;
        }

        return dict.Count;
    }

    /// <summary>
    /// FNV-1a hash: Excellent distribution for byte sequences
    /// Used in Level 4 implementation
    /// Best balance of speed and quality
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("HashAlgorithm")]
    public int FNV1aHash_Dictionary()
    {
        var dict = new Dictionary<int, int>();

        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = ComputeFNV1aHash(bytes);

            if (dict.TryGetValue(hash, out var count))
                dict[hash] = count + 1;
            else
                dict[hash] = 1;
        }

        return dict.Count;
    }

    /// <summary>
    /// Just hash computation overhead (no dictionary)
    /// Shows pure hash algorithm performance
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("PureHash")]
    public long PureStringHashCode()
    {
        long sum = 0;
        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = Encoding.UTF8.GetString(bytes).GetHashCode();
            sum += hash;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("PureHash")]
    public long PureSimpleHash()
    {
        long sum = 0;
        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = ComputeSimpleHash(bytes);
            sum += hash;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("PureHash")]
    public long PureFNV1aHash()
    {
        long sum = 0;
        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = ComputeFNV1aHash(bytes);
            sum += hash;
        }
        return sum;
    }

    // Hash implementations

    private static int ComputeSimpleHash(ReadOnlySpan<byte> bytes)
    {
        unchecked
        {
            var hash = 0;
            foreach (var b in bytes)
            {
                hash = hash * 31 + b;
            }
            return hash;
        }
    }

    private static int ComputeFNV1aHash(ReadOnlySpan<byte> bytes)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
