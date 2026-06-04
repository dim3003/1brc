using BenchmarkDotNet.Attributes;
using System.Text;

namespace Benchmarks.Level5_Simd;

/// <summary>
/// Benchmark: Hash Functions Comparison for Station Name Lookup
/// 
/// Tests: SimpleHash (hash*33) vs FNV-1a 32-bit vs FNV-1a 64-bit (2x unroll) vs xxHash32
/// Purpose: Determine optimal hash function for 413 stations
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[BenchmarkCategory("Level05", "HashFunctions")]
public class HashFunctionsBenchmark
{
    private byte[][] _stationNames = [];
    private const int StationCount = 413;
    private const int LookupCount = 1_000_000;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _stationNames = new byte[StationCount][];
        
        for (var i = 0; i < StationCount; i++)
        {
            var name = $"Station_{i:D4}_Name";
            _stationNames[i] = Encoding.UTF8.GetBytes(name);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SimpleHash")]
    public uint SimpleHash()
    {
        uint total = 0;
        for (var i = 0; i < LookupCount; i++)
        {
            var data = _stationNames[i % StationCount];
            var hash = 0;
            foreach (var b in data)
            {
                hash = ((hash << 5) + hash) ^ b;  // hash * 33 + b
            }
            total += (uint)hash;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("FNV1a")]
    public uint FNV1aHash()
    {
        uint total = 0;
        for (var i = 0; i < LookupCount; i++)
        {
            var data = _stationNames[i % StationCount];
            var hash = 2166136261u;  // FNV offset basis
            foreach (var b in data)
            {
                hash ^= b;
                hash *= 16777619u;  // FNV prime
            }
            total += hash;
        }
        return total;
    }

    /// <summary>
    /// FNV-1a 64-bit with 2x loop unroll — used in Level 6 (Expert) and Level 7 (Ultimate).
    /// Starting value 0 (non-standard offset basis), prime 1099511628211 (0x00000100000001B3).
    /// 2x unroll halves branch/counter overhead per byte.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FNV1a64")]
    public ulong FNV1a64Hash_2xUnroll()
    {
        ulong total = 0;
        for (var i = 0; i < LookupCount; i++)
        {
            var data = _stationNames[i % StationCount];
            var hash = 0ul;
            var j = 0;
            for (; j + 1 < data.Length; j += 2)
            {
                hash ^= data[j];
                hash *= 1099511628211ul;
                hash ^= data[j + 1];
                hash *= 1099511628211ul;
            }
            if (j < data.Length)
            {
                hash ^= data[j];
                hash *= 1099511628211ul;
            }
            total += hash;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("XxHash")]
    public uint XxHash32()
    {
        uint total = 0;
        for (var i = 0; i < LookupCount; i++)
        {
            var data = _stationNames[i % StationCount];
            var hash = XxHash32Compute(data);
            total += hash;
        }
        return total;
    }

    private static uint XxHash32Compute(byte[] data)
    {
        const uint Prime1 = 2654435761u;
        const uint Prime2 = 2246822519u;
        const uint Prime3 = 3266489917u;
        const uint Prime4 = 668265263u;
        const uint Prime5 = 374761393u;

        var h32 = Prime5 + (uint)data.Length;
        
        var i = 0;
        for (; i <= data.Length - 4; i += 4)
        {
            var k1 = BitConverter.ToUInt32(data, i);
            k1 *= Prime3;
            h32 ^= k1;
            h32 = (h32 << 17) | (h32 >> 15);
            h32 *= Prime4;
        }

        for (; i < data.Length; i++)
        {
            h32 ^= data[i] * Prime5;
            h32 = ((h32 << 11) | (h32 >> 21)) * Prime1;
        }

        h32 ^= h32 >> 15;
        h32 *= Prime2;
        h32 ^= h32 >> 13;
        h32 *= Prime3;
        h32 ^= h32 >> 16;

        return h32;
    }
}
