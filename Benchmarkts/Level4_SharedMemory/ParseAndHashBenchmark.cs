using BenchmarkDotNet.Attributes;
using System.Buffers.Text;
using System.Text;

namespace Benchmarks.Level4_SharedMemory;


/* Test Results
 
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8524/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 7950X 4.50GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.3.26207.106
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

| Method      | RawValue | Mean      | Error     | StdDev    |
|------------ |--------- |----------:|----------:|----------:|
| CustomParse | -12.7    |  3.137 ns | 1.0576 ns | 0.1637 ns |
| Utf8Parse   | -12.7    | 19.875 ns | 4.9043 ns | 0.7590 ns |
| SpanParse   | -12.7    | 33.937 ns | 8.2457 ns | 2.1414 ns |
|             |          |           |           |           |
| CustomParse | -9.5     |  2.411 ns | 0.1504 ns | 0.0233 ns |
| Utf8Parse   | -9.5     | 19.078 ns | 2.5603 ns | 0.6649 ns |
| SpanParse   | -9.5     | 29.743 ns | 4.4084 ns | 1.1448 ns |
|             |          |           |           |           |
| CustomParse | 32.4     |  2.895 ns | 0.3547 ns | 0.0549 ns |
| Utf8Parse   | 32.4     | 20.486 ns | 7.3962 ns | 1.1446 ns |
| SpanParse   | 32.4     | 31.601 ns | 7.1917 ns | 1.8677 ns |
|             |          |           |           |           |
| CustomParse | 9.1      |  2.420 ns | 0.6546 ns | 0.1700 ns |
| Utf8Parse   | 9.1      | 17.715 ns | 2.4766 ns | 0.3833 ns |
| SpanParse   | 9.1      | 32.133 ns | 6.7557 ns | 1.7544 ns |


 */



// =============================================================================
// Benchmark 1: Temperature Parse
//
// Compare the custom ParseTemperature method from Level4_SharedMemory with
// .NET built-in alternatives.
//
// Methods:
//   CustomParse  → Hand-written byte parser (implementation from Level4)
//                  Exploits format knowledge: always 1 decimal place
//   Utf8Parse    → System.Buffers.Text.Utf8Parser — built-in, directly from byte
//                  General purpose, no format optimization
//   SpanParse    → UTF-8 byte → stackalloc char[] → double.Parse
//                  No heap allocation but UTF-8 decode step exists
//
// Expected order:
//   CustomParse < Utf8Parse < SpanParse
//   (CustomParse has minimal branching and loops thanks to format assumptions)
// =============================================================================
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5, launchCount: 1)]
[BenchmarkCategory("Level04", "Parse")]
public class TemperatureParsingBenchmark
{
    // All format variants in 1BRC:
    //   D.D   → single digit integer, single decimal    ("9.1")
    //   DD.D  → two digit integer, single decimal       ("32.4")
    //   -D.D  → negative, single digit                  ("-9.5")
    //   -DD.D → negative, two digits                    ("-12.7")
    [Params("9.1", "32.4", "-9.5", "-12.7")]
    public string RawValue { get; set; } = string.Empty;

    private byte[] _bytes = [];

    [GlobalSetup]
    public void Setup() => _bytes = Encoding.UTF8.GetBytes(RawValue);

    /// <summary>
    /// Hand-written byte parser — implementation from Level4_SharedMemory.
    /// Not a general parser since it's known that there's always 1 decimal place.
    /// No allocation.
    /// </summary>
    [Benchmark(Baseline = true)]
    public double CustomParse() => ParseTemperature(_bytes);

    /// <summary>
    /// .NET built-in UTF-8 parser — parses double directly from a byte span.
    /// No string allocation but cannot use CustomParse's format shortcuts.
    /// </summary>
    [Benchmark]
    public double Utf8Parse()
    {
        Utf8Parser.TryParse(_bytes, out double value, out _);
        return value;
    }

    /// <summary>
    /// UTF-8 byte → char (stackalloc, no heap) → double.Parse(Span).
    /// double.Parse(ReadOnlySpan&lt;char&gt;) is managed but SIMD-accelerated.
    /// The UTF-8 decode step adds extra cost.
    /// </summary>
    [Benchmark]
    public double SpanParse()
    {
        Span<char> chars = stackalloc char[_bytes.Length];
        var count = Encoding.UTF8.GetChars(_bytes, chars);
        return double.Parse(chars[..count]);
    }

    // -------------------------------------------------------------------------
    // Copied from Level4_SharedMemory\Program.cs (local static -> private static)
    // -------------------------------------------------------------------------
    private static double ParseTemperature(ReadOnlySpan<byte> span)
    {
        if (span.Length > 0 && span[^1] == '\r')
            span = span[..^1];

        var negative = false;
        var index    = 0;

        if (span[0] == '-')
        {
            negative = true;
            index    = 1;
        }

        double result       = 0;
        var decimalFound    = false;
        var decimalPlace    = 0.1;

        while (index < span.Length)
        {
            var c = span[index++];

            if (c == '.')
            {
                decimalFound = true;
                continue;
            }

            var digit = c - '0';

            if (decimalFound)
            {
                result      += digit * decimalPlace;
                decimalPlace *= 0.1;
            }
            else
            {
                result = result * 10 + digit;
            }
        }

        return negative ? -result : result;
    }
}