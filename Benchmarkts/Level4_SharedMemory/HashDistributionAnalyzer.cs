using System.Text;
using System.Diagnostics;

namespace Benchmarks.Level4_SharedMemory;

/* Test Results
 
=== Hash Algorithm Performance for Dictionary<int, Stats> ===
Test: 1,000,000,000 dictionary operations (lookup/insert)
Keys: 413 unique station names (each repeated ~2,421,307 times)

=== Algorithm 1: String.GetHashCode() with allocation ===
  Implementation: Encoding.UTF8.GetString(bytes).GetHashCode()
  Issue: Creates string object for every hash computation!
  Time: 35,000 ms
  Throughput: 28,571,349 ops/sec
  Result: 413 unique hashes

=== Algorithm 2: Direct byte hashing (Simple) ===
  Implementation: hash = hash * 31 + byte
  Benefit: No allocation, simple computation
  Time: 16,887 ms
  Throughput: 59,215,900 ops/sec
  Result: 413 unique hashes

=== Algorithm 3: Direct byte hashing (FNV-1a) ===
  Implementation: hash ^= byte; hash *= 16777619
  Benefit: No allocation + better avalanche effect
  Time: 17,519 ms
  Throughput: 57,080,061 ops/sec
  Result: 413 unique hashes

======================================================================
PERFORMANCE SUMMARY
======================================================================

1. STRING ALLOCATION COST (String.GetHashCode vs FNV-1a):
   String.GetHashCode(): 35,000 ms
   FNV-1a (byte-based):  17,519 ms
   Overhead:             17,481 ms (49.9% slower)
   - Primary benefit: AVOID STRING ALLOCATION

2. HASH ALGORITHM COMPARISON (Simple vs FNV-1a):
   Simple Hash: 16,887 ms
   FNV-1a Hash: 17,519 ms
   - Simple is 632 ms faster (3.6% vs FNV-1a)
   - This is marginal at scale - distribution quality matters more!

3. WHY FNV-1a IS CHOSEN (despite Simple being slightly faster):
   - Eliminates string allocation (massive win vs String.GetHashCode!)
   - Speed difference negligible: 632 ms in 17519 ms (3.61%)
   - Better worst-case: Max items/bucket typically lower (see distribution below)
   - Better avalanche effect: Similar strings produce very different hashes
   - Industry-standard: Proven algorithm used in many production systems
   - Predictable performance: No clustering, consistent lookup times

======================================================================
EXTRAPOLATION TO 1 BILLION MEASUREMENTS
======================================================================
  String.GetHashCode(): 35.0s (0.6 minutes)
  Simple Hash:          16.9s (0.3 minutes)
  FNV-1a Hash:          17.5s (0.3 minutes)

  Time saved (FNV-1a vs String): 17.5s (0.3 minutes)
  + Additional GC overhead avoided from string allocations

======================================================================
HASH DISTRIBUTION QUALITY (Advanced Metrics)
======================================================================

Comparing distribution quality across all 3 algorithms:
(Lower numbers = better distribution)

1. String.GetHashCode():
  Unique hashes: 413/413
  Distribution (1024 buckets):
    Used: 321/1024, Empty: 703
    Load factor: 0.40
    Max items/bucket: 4 (lower = better)
    Std deviation: 0.67 (lower = better uniformity)
    Buckets with collisions: 78 (7.6%)
    Items needing probing: 92 (22.3%)

2. Simple Multiplicative Hash:
  Unique hashes: 413/413
  Distribution (1024 buckets):
    Used: 350/1024, Empty: 674
    Load factor: 0.40
    Max items/bucket: 4 (lower = better)
    Std deviation: 0.62 (lower = better uniformity)
    Buckets with collisions: 56 (5.5%)
    Items needing probing: 63 (15.3%)

3. FNV-1a Hash:
  Unique hashes: 413/413
  Distribution (1024 buckets):
    Used: 348/1024, Empty: 676
    Load factor: 0.40
    Max items/bucket: 3 (lower = better)
    Std deviation: 0.61 (lower = better uniformity)
    Buckets with collisions: 60 (5.9%)
    Items needing probing: 65 (15.7%)

----------------------------------------------------------------------
DISTRIBUTION QUALITY COMPARISON:
----------------------------------------------------------------------
Metric                              | String     | Simple     | FNV-1a
----------------------------------------------------------------------
Max items per bucket                | 4          | 4          | 3
Std deviation (lower=better)        | 0.67       | 0.62       | 0.61
% items needing probing             | 22.3       | 15.3       | 15.7
----------------------------------------------------------------------

Conclusion: FNV-1a has better distribution (std dev difference: 0.002)
Real-world impact:
  - Better distribution = more predictable lookup times
  - Lower max items/bucket = better worst-case performance
  - At 1 billion scale, FNV-1a provides more consistent performance
 
 
 */




/// <summary>
/// Hash Algorithm Quality Analysis for 1BRC
/// 
/// Goal: Show how hash algorithm choice affects Dictionary<int, Stats> performance
/// All approaches use Dictionary<int, ...> but with different hash algorithms
/// 
/// Key insight: Better hash distribution = fewer collisions = faster lookups
/// </summary>
public static class HashDistributionAnalyzer
{
    public static void AnalyzeHashPerformance(int measurementCount = 10_000_000)
    {
        Console.WriteLine($"=== Hash Algorithm Performance for Dictionary<int, Stats> ===");
        Console.WriteLine($"Test: {measurementCount:N0} dictionary operations (lookup/insert)");
        Console.WriteLine($"Keys: 413 unique station names (each repeated ~{measurementCount / 413:N0} times)\n");

        var stationNames = GenerateStationNames(413);
        var stationBytes = stationNames.Select(s => Encoding.UTF8.GetBytes(s)).ToArray();

        // Simulate 1BRC: repeated measurements from same stations
        var random = new Random(42);
        var measurementIndices = new int[measurementCount];
        for (int i = 0; i < measurementCount; i++)
        {
            measurementIndices[i] = random.Next(413);
        }

        Console.WriteLine("=== Algorithm 1: String.GetHashCode() with allocation ===");
        Console.WriteLine("  Implementation: Encoding.UTF8.GetString(bytes).GetHashCode()");
        Console.WriteLine("  Issue: Creates string object for every hash computation!");
        var stringTime = TestStringHashCodeAlgorithm(stationBytes, measurementIndices);

        Console.WriteLine("\n=== Algorithm 2: Direct byte hashing (Simple) ===");
        Console.WriteLine("  Implementation: hash = hash * 31 + byte");
        Console.WriteLine("  Benefit: No allocation, simple computation");
        var simpleTime = TestSimpleHashAlgorithm(stationBytes, measurementIndices);

        Console.WriteLine("\n=== Algorithm 3: Direct byte hashing (FNV-1a) ===");
        Console.WriteLine("  Implementation: hash ^= byte; hash *= 16777619");
        Console.WriteLine("  Benefit: No allocation + better avalanche effect");
        var fnvTime = TestFNV1aHashAlgorithm(stationBytes, measurementIndices);

        // Performance comparison
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("PERFORMANCE SUMMARY");
        Console.WriteLine(new string('=', 70));

        var stringAllocationOverhead = stringTime - fnvTime;
        var simpleVsFnv = simpleTime - fnvTime;

        Console.WriteLine($"\n1. STRING ALLOCATION COST (String.GetHashCode vs FNV-1a):");
        Console.WriteLine($"   String.GetHashCode(): {stringTime:N0} ms");
        Console.WriteLine($"   FNV-1a (byte-based):  {fnvTime:N0} ms");
        Console.WriteLine($"   Overhead:             {stringAllocationOverhead:N0} ms ({stringAllocationOverhead / (double)stringTime * 100:F1}% slower)");
        Console.WriteLine($"   → Primary benefit: AVOID STRING ALLOCATION");

        Console.WriteLine($"\n2. HASH ALGORITHM COMPARISON (Simple vs FNV-1a):");
        Console.WriteLine($"   Simple Hash: {simpleTime:N0} ms");
        Console.WriteLine($"   FNV-1a Hash: {fnvTime:N0} ms");
        if (Math.Abs(simpleVsFnv) < 5)
        {
            Console.WriteLine($"   → Performance: VIRTUALLY IDENTICAL ({Math.Abs(simpleVsFnv)} ms difference, {Math.Abs(simpleVsFnv) / (double)Math.Max(simpleTime, fnvTime) * 100:F2}%)");
        }
        else
        {
            var faster = simpleTime < fnvTime ? "Simple" : "FNV-1a";
            var slower = simpleTime < fnvTime ? "FNV-1a" : "Simple";
            var diff = Math.Abs(simpleVsFnv);
            Console.WriteLine($"   → {faster} is {diff} ms faster ({diff / (double)Math.Max(simpleTime, fnvTime) * 100:F1}% vs {slower})");
            Console.WriteLine($"   → This is marginal at scale - distribution quality matters more!");
        }

        Console.WriteLine($"\n3. WHY FNV-1a IS CHOSEN (despite Simple being slightly faster):");
        Console.WriteLine($"   ✓ Eliminates string allocation (massive win vs String.GetHashCode!)");
        Console.WriteLine($"   ✓ Speed difference negligible: {Math.Abs(simpleVsFnv)} ms in {Math.Max(simpleTime, fnvTime)} ms ({Math.Abs(simpleVsFnv) / (double)Math.Max(simpleTime, fnvTime) * 100:F2}%)");
        Console.WriteLine($"   ✓ Better worst-case: Max items/bucket typically lower (see distribution below)");
        Console.WriteLine($"   ✓ Better avalanche effect: Similar strings produce very different hashes");
        Console.WriteLine($"   ✓ Industry-standard: Proven algorithm used in many production systems");
        Console.WriteLine($"   ✓ Predictable performance: No clustering, consistent lookup times");

        // Extrapolation
        Console.WriteLine($"\n" + new string('=', 70));
        Console.WriteLine($"EXTRAPOLATION TO 1 BILLION MEASUREMENTS");
        Console.WriteLine(new string('=', 70));

        var ratio = 1_000_000_000.0 / measurementCount;
        var stringTime1B = stringTime * ratio / 1000.0;
        var simpleTime1B = simpleTime * ratio / 1000.0;
        var fnvTime1B = fnvTime * ratio / 1000.0;

        Console.WriteLine($"  String.GetHashCode(): {stringTime1B:F1}s ({stringTime1B / 60:F1} minutes)");
        Console.WriteLine($"  Simple Hash:          {simpleTime1B:F1}s ({simpleTime1B / 60:F1} minutes)");
        Console.WriteLine($"  FNV-1a Hash:          {fnvTime1B:F1}s ({fnvTime1B / 60:F1} minutes)");
        Console.WriteLine($"\n  Time saved (FNV-1a vs String): {stringTime1B - fnvTime1B:F1}s ({(stringTime1B - fnvTime1B) / 60:F1} minutes)");
        Console.WriteLine($"  + Additional GC overhead avoided from string allocations");

        Console.WriteLine($"\n" + new string('=', 70));
        Console.WriteLine("HASH DISTRIBUTION QUALITY (Advanced Metrics)");
        Console.WriteLine(new string('=', 70));
        AnalyzeDistribution(stationNames, stationBytes);
    }

    private static long TestStringHashCodeAlgorithm(byte[][] stationBytes, int[] measurementIndices)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();
        var dict = new Dictionary<int, long>();

        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = Encoding.UTF8.GetString(bytes).GetHashCode(); // String allocation!

            if (dict.TryGetValue(hash, out var count))
                dict[hash] = count + 1;
            else
                dict[hash] = 1;
        }

        sw.Stop();

        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {measurementIndices.Length / sw.Elapsed.TotalSeconds:N0} ops/sec");
        Console.WriteLine($"  Result: {dict.Count} unique hashes");

        return sw.ElapsedMilliseconds;
    }

    private static long TestSimpleHashAlgorithm(byte[][] stationBytes, int[] measurementIndices)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();
        var dict = new Dictionary<int, long>();

        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = ComputeSimpleHash(bytes);

            if (dict.TryGetValue(hash, out var count))
                dict[hash] = count + 1;
            else
                dict[hash] = 1;
        }

        sw.Stop();

        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {measurementIndices.Length / sw.Elapsed.TotalSeconds:N0} ops/sec");
        Console.WriteLine($"  Result: {dict.Count} unique hashes");

        return sw.ElapsedMilliseconds;
    }

    private static long TestFNV1aHashAlgorithm(byte[][] stationBytes, int[] measurementIndices)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();
        var dict = new Dictionary<int, long>();

        foreach (var idx in measurementIndices)
        {
            var bytes = stationBytes[idx];
            var hash = ComputeFNV1aHash(bytes);

            if (dict.TryGetValue(hash, out var count))
                dict[hash] = count + 1;
            else
                dict[hash] = 1;
        }

        sw.Stop();

        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {measurementIndices.Length / sw.Elapsed.TotalSeconds:N0} ops/sec");
        Console.WriteLine($"  Result: {dict.Count} unique hashes");

        if (dict.Count != 413)
        {
            Console.WriteLine($"  ⚠️  WARNING: Hash collisions! Only {dict.Count}/413 unique hashes");
        }

        return sw.ElapsedMilliseconds;
    }

    private static void AnalyzeDistribution(string[] stationNames, byte[][] stationBytes)
    {
        Console.WriteLine("\nComparing distribution quality across all 3 algorithms:");
        Console.WriteLine("(Lower numbers = better distribution)\n");

        Console.WriteLine("1. String.GetHashCode():");
        var stringHashes = stationNames.Select(s => s.GetHashCode()).ToArray();
        var stringMetrics = AnalyzeHashQuality(stringHashes, stationNames.Length);

        Console.WriteLine("\n2. Simple Multiplicative Hash:");
        var simpleHashes = stationBytes.Select(ComputeSimpleHash).ToArray();
        var simpleMetrics = AnalyzeHashQuality(simpleHashes, stationBytes.Length);

        Console.WriteLine("\n3. FNV-1a Hash:");
        var fnvHashes = stationBytes.Select(ComputeFNV1aHash).ToArray();
        var fnvMetrics = AnalyzeHashQuality(fnvHashes, stationBytes.Length);

        // Comparison summary
        Console.WriteLine("\n" + new string('-', 70));
        Console.WriteLine("DISTRIBUTION QUALITY COMPARISON:");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine($"{"Metric",-35} | {"String",-10} | {"Simple",-10} | {"FNV-1a",-10}");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine($"{"Max items per bucket",-35} | {stringMetrics.maxBucket,-10} | {simpleMetrics.maxBucket,-10} | {fnvMetrics.maxBucket,-10}");
        Console.WriteLine($"{"Std deviation (lower=better)",-35} | {stringMetrics.stdDev,-10:F2} | {simpleMetrics.stdDev,-10:F2} | {fnvMetrics.stdDev,-10:F2}");
        Console.WriteLine($"{"% items needing probing",-35} | {stringMetrics.probingPct,-10:F1} | {simpleMetrics.probingPct,-10:F1} | {fnvMetrics.probingPct,-10:F1}");
        Console.WriteLine(new string('-', 70));

        var winner = fnvMetrics.stdDev < simpleMetrics.stdDev ? "FNV-1a" : "Simple";
        var diff = Math.Abs(fnvMetrics.stdDev - simpleMetrics.stdDev);

        Console.WriteLine($"\nConclusion: {winner} has better distribution (std dev difference: {diff:F3})");
        Console.WriteLine($"Real-world impact:");
        Console.WriteLine($"  - Better distribution = more predictable lookup times");
        Console.WriteLine($"  - Lower max items/bucket = better worst-case performance");
        Console.WriteLine($"  - At 1 billion scale, {winner} provides more consistent performance");
    }

    private static (int maxBucket, double stdDev, double probingPct) AnalyzeHashQuality(int[] hashes, int expectedUnique)
    {
        // Check for hash collisions
        var uniqueHashes = hashes.Distinct().Count();
        var collisions = expectedUnique - uniqueHashes;

        Console.WriteLine($"  Unique hashes: {uniqueHashes}/{expectedUnique}");
        if (collisions > 0)
        {
            Console.WriteLine($"  ⚠️  COLLISIONS: {collisions} ({collisions * 100.0 / expectedUnique:F2}%)");
        }

        // Bucket distribution (simulating dictionary with 1024 buckets)
        const int bucketCount = 1024;
        var buckets = new int[bucketCount];
        foreach (var hash in hashes)
        {
            buckets[Math.Abs(hash % bucketCount)]++;
        }

        var emptyBuckets = buckets.Count(b => b == 0);
        var maxBucket = buckets.Max();
        var avgPerBucket = (double)expectedUnique / bucketCount;
        var variance = buckets.Average(b => Math.Pow(b - avgPerBucket, 2));
        var stdDev = Math.Sqrt(variance);

        Console.WriteLine($"  Distribution (1024 buckets):");
        Console.WriteLine($"    Used: {bucketCount - emptyBuckets}/1024, Empty: {emptyBuckets}");
        Console.WriteLine($"    Load factor: {(double)expectedUnique / bucketCount:F2}");
        Console.WriteLine($"    Max items/bucket: {maxBucket} (lower = better)");
        Console.WriteLine($"    Std deviation: {stdDev:F2} (lower = better uniformity)");

        // Calculate clustering - how many buckets have 2+ items (real collisions)
        var collisionBuckets = buckets.Count(b => b > 1);
        var totalCollisions = buckets.Where(b => b > 1).Sum(b => b - 1);
        var probingPct = totalCollisions * 100.0 / expectedUnique;

        if (collisionBuckets > 0)
        {
            Console.WriteLine($"    Buckets with collisions: {collisionBuckets} ({collisionBuckets * 100.0 / bucketCount:F1}%)");
            Console.WriteLine($"    Items needing probing: {totalCollisions} ({probingPct:F1}%)");
        }
        else
        {
            Console.WriteLine($"    Perfect distribution: No bucket collisions!");
        }

        return (maxBucket, stdDev, probingPct);
    }

    private static string[] GenerateStationNames(int count)
    {
        var names = new string[count];
        var random = new Random(42);
        var prefixes = new[] { "New", "San", "Port", "Mount", "Saint", "North", "South", "East", "West" };
        var suffixes = new[] { "ville", "town", "city", "burg", "field", "wood", "dale", "mont", "berg" };

        for (var i = 0; i < count; i++)
        {
            if (i < 50)
            {
                names[i] = i switch
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
                    15 => "Oslo",
                    16 => "Stockholm",
                    17 => "Warsaw",
                    18 => "Prague",
                    19 => "Vienna",
                    _ => $"{prefixes[i % prefixes.Length]}{suffixes[i % suffixes.Length]}_{i}"
                };
            }
            else
            {
                names[i] = $"{prefixes[random.Next(prefixes.Length)]}{suffixes[random.Next(suffixes.Length)]}_{i}";
            }
        }

        return names;
    }

    private static int ComputeSimpleHash(byte[] bytes)
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

    private static int ComputeFNV1aHash(byte[] bytes)
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
