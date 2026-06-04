using BenchmarkDotNet.Attributes;

namespace Benchmarkts.Level2_Stream;

/// <summary>
/// Benchmark: String.Split vs IndexOf Parsing
/// 
/// Tests: Split() allocation vs manual IndexOf parsing
/// Purpose: Show allocation cost of Split() in hot path
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Level2", "Parsing")]
public class SplitVsIndexOfBenchmark
{
    private string[] _lines = [];
    private const int LineCount = 100_000;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(1907);
        var stations = new[] { "Berlin", "London", "Paris", "Tokyo", "NewYork" };
        
        _lines = new string[LineCount];
        for (var i = 0; i < LineCount; i++)
        {
            var station = stations[random.Next(stations.Length)];
            var temp = random.NextDouble() * 40 - 10;
            _lines[i] = $"{station};{temp:F1}";
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Split")]
    public int UsingSplit()
    {
        var count = 0;
        foreach (var line in _lines)
        {
            var parts = line.Split(';');
            var station = parts[0];
            var temp = double.Parse(parts[1]);
            count += station.Length;
        } 
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("IndexOf")]
    public int UsingIndexOf()
    {
        var count = 0;
        foreach (var line in _lines)
        {
            var idx = line.IndexOf(';');
            var station = line.Substring(0, idx);
            var temp = double.Parse(line.Substring(idx + 1));
            count += station.Length;
        }
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Span")]
    public int UsingSpan()
    {
        var count = 0;
        foreach (var line in _lines)
        {
            var idx = line.IndexOf(';');
            ReadOnlySpan<char> station = line.AsSpan(0, idx);
        
            var temp = double.Parse(line.AsSpan(idx + 1));
            count += station.Length;
        }

        return count;
    }
}
