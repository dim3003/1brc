using BenchmarkDotNet.Attributes;

namespace Benchmarks.Level4_SharedMemory;


/*
 Test Results;

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 7950X 4.50GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.3.26207.106
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  Job-WWFMMQ : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4

IterationCount=3  WarmupCount=1

| Method          | Mean       |
|---------------- |-----------:|
| SmallSequential |   1.087 ms |
| LargeSequential |   8.538 ms |
| SmallRandom     |  18.753 ms |
| LargeRandom     | 210.469 ms |


 */



/// <summary>
/// Benchmark: CPU Cache vs RAM Performance
///
/// Memory Hierarchy:
///   - L1 Cache:  ~32-64 KB per core, ~1 ns latency
///   - L2 Cache:  ~512 KB - 1 MB per core, ~3-10 ns latency  
///   - L3 Cache:  64 MB shared (Ryzen 9 7950X), ~20-40 ns latency
///   - RAM:       Unbounded, ~80-100 ns latency
///
/// Tests:
///   1. Small data (32MB total with indices): Sequential access = CPU cache hit
///   2. Small data (32MB total with indices): Random access = CPU cache hit (warmed up)
///   3. Large data (4x L3): Sequential access = RAM but prefetcher helps
///   4. Large data (4x L3): Random access = RAM miss, no prefetch = slowest
///
/// Note: Random access needs indices array, so total memory = data + indices
///       Small: 16MB data + 16MB indices = 32MB total (fits comfortably in 64MB L3)
///       Large: 128MB data + 128MB indices = 256MB total (4x L3, guaranteed RAM access)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
[BenchmarkCategory("Level04", "MemoryAccess")]
public class SequentialVsRandomAccessBenchmark
{
    // Small: Fits in L3 Cache including indices
    // 4M ints = 16MB data + 16MB indices = 32MB total (half of 64MB L3, safe margin)
    private const int SmallSize = 4 * 1024 * 1024;

    // Large: Far exceeds L3 Cache
    // 32M ints = 128MB data + 128MB indices = 256MB total (4x L3, guaranteed RAM)
    private const int LargeSize = 32 * 1024 * 1024;

    private int[] smallData = [];
    private int[] largeData = [];

    private int[] smallRandomIndices = [];
    private int[] largeRandomIndices = [];

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        // Small data (32MB - fits in L3)
        smallData = new int[SmallSize];
        for (var i = 0; i < SmallSize; i++)
            smallData[i] = rng.Next();

        // Large data (128MB - exceeds L3)
        largeData = new int[LargeSize];
        for (var i = 0; i < LargeSize; i++)
            largeData[i] = rng.Next();

        // Generate random indices
        smallRandomIndices = GenerateRandomIndices(SmallSize, rng);
        largeRandomIndices = GenerateRandomIndices(LargeSize, rng);
    }

    private static int[] GenerateRandomIndices(int size, Random rng)
    {
        var indices = new int[size];
        for (var i = 0; i < size; i++)
            indices[i] = i;

        // Fisher-Yates shuffle
        for (var i = size - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }

    /// <summary>
    /// Sequential access, small data (32MB total fits in L3 cache)
    /// Data: 16MB, no indices needed for sequential
    /// Result: CPU cache hit + prefetcher = fastest
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Sequential", "CacheHit")]
    public long SmallSequential()
    {
        long sum = 0;
        var data = smallData;

        for (var i = 0; i < data.Length; i++)
            sum += data[i];

        return sum;
    }

    /// <summary>
    /// Random access, small data (32MB total: 16MB data + 16MB indices = fits in L3)
    /// Result: CPU cache hit but no prefetching
    /// Should be ~3-5x slower than SmallSequential but still fast (L3 hit)
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Random", "CacheHit")]
    public long SmallRandom()
    {
        long sum = 0;
        var data = smallData;
        var indices = smallRandomIndices;

        for (var i = 0; i < indices.Length; i++)
            sum += data[indices[i]];

        return sum;
    }

    /// <summary>
    /// Sequential access, large data (256MB total exceeds L3 cache)
    /// Data: 128MB
    /// Result: RAM access but prefetcher helps a lot
    /// Streaming read pattern is efficient even from RAM
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Sequential", "RAMAccess")]
    public long LargeSequential()
    {
        long sum = 0;
        var data = largeData;

        for (var i = 0; i < data.Length; i++)
            sum += data[i];

        return sum;
    }

    /// <summary>
    /// Random access, large data (256MB total: 128MB data + 128MB indices = 4x L3)
    /// Result: RAM cache miss + no prefetching = slowest
    /// Every access is a RAM round trip with ~100ns latency
    /// Expected: ~20-30x slower than LargeSequential
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Random", "RAMAccess")]
    public long LargeRandom()
    {
        long sum = 0;
        var data = largeData;
        var indices = largeRandomIndices;

        for (var i = 0; i < indices.Length; i++)
            sum += data[indices[i]];

        return sum;
    }
}
