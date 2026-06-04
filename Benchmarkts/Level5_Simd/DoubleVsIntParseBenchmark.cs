using BenchmarkDotNet.Attributes;
using System.Text;

namespace Benchmarks.Level5_Simd;


/* Benchmark Results;


| Method               | RawValue | Mean      | Error     | StdDev    | Ratio |
|--------------------- |--------- |----------:|----------:|----------:|------:|
| DoubleParse          | -12.7    | 2.9380 ns | 1.0126 ns | 0.2630 ns |  1.01 |
| IntParseBranchless   | -12.7    | 0.9081 ns | 0.1584 ns | 0.0411 ns |  0.31 |
| IntParseWithDivision | -12.7    | 1.1209 ns | 0.8821 ns | 0.1365 ns |  0.38 |
|                      |          |           |           |           |       |
| DoubleParse          | -9.5     | 2.4770 ns | 1.1279 ns | 0.2929 ns |  1.01 |
| IntParseBranchless   | -9.5     | 0.6665 ns | 0.2835 ns | 0.0736 ns |  0.27 |
| IntParseWithDivision | -9.5     | 0.7975 ns | 0.5857 ns | 0.1521 ns |  0.33 |
|                      |          |           |           |           |       |
| DoubleParse          | 32.4     | 3.0304 ns | 1.5390 ns | 0.3997 ns |  1.01 |
| IntParseBranchless   | 32.4     | 0.8328 ns | 0.5208 ns | 0.1352 ns |  0.28 |
| IntParseWithDivision | 32.4     | 0.8936 ns | 0.0636 ns | 0.0098 ns |  0.30 |
|                      |          |           |           |           |       |
| DoubleParse          | 9.1      | 2.8623 ns | 0.3156 ns | 0.0820 ns |  1.00 |
| IntParseBranchless   | 9.1      | 0.5690 ns | 0.4542 ns | 0.1179 ns |  0.20 |
| IntParseWithDivision | 9.1      | 0.7308 ns | 0.5316 ns | 0.1381 ns |  0.26 |


 */



// =============================================================================
// Double vs Int Temperature Parsing
//
// Comparison of Level4 (double-based) vs Level5 (int-based) parsing.
//
// Key Difference:
//
//   Level4 - DoubleParse:
//     * Advances byte by byte in a loop
//     * Floating-point multiplication for each decimal digit: result += digit * decimalPlace
//     * decimalPlace *= 0.1  -> float operation, repeated every iteration
//     * General-purpose (handles any number of decimal digits)
//
//   Level5 - IntParseBranchless:
//     * NO loop, direct index access (ptr[0], ptr[1], ptr[2]...)
//     * Integer arithmetic only (*10, *100, +)
//     * Result returned as temperature x 10 (12.3 -> 123, -9.5 -> -95)
//     * Exploits format knowledge: always exactly 1 decimal digit
//
// Extra Scenario - IntParseWithDivision:
//     * Total cost when the Level5 result needs to be converted back to double
//     * int result / 10.0 -> includes the double conversion overhead
//
// Expected Order:
//   IntParseBranchless < IntParseWithDivision < DoubleParse
//
//   Why?
//     * Integer arithmetic is faster than float on the CPU (FPU pipeline bypass)
//     * No loop -> no branch predictor pressure, no loop overhead
//     * Direct indexing -> memory access is fixed and predictable
// =============================================================================
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5, launchCount: 1)]
[BenchmarkCategory("Level05", "Parse")]
public class DoubleVsIntParseBenchmark
{
    // 1BRC format variants:
    //   len=3 -> "D.D"    e.g. "9.1"   -> int: 91
    //   len=4 -> "DD.D"   e.g. "32.4"  -> int: 324
    //   len=4 -> "-D.D"   e.g. "-9.5"  -> int: -95
    //   len=5 -> "-DD.D"  e.g. "-12.7" -> int: -127
    [Params("9.1", "32.4", "-9.5", "-12.7")]
    public string RawValue { get; set; } = string.Empty;

    private byte[] _bytes = [];

    [GlobalSetup]
    public void Setup() => _bytes = Encoding.UTF8.GetBytes(RawValue);

    /// <summary>
    /// Level4 implementation: byte-by-byte loop + floating-point arithmetic.
    /// Each iteration includes a float multiplication and a decimalPlace update.
    /// </summary>
    [Benchmark(Baseline = true)]
    public double DoubleParse() => ParseTemperatureDouble(_bytes);

    /// <summary>
    /// Level5 implementation: no loop, direct indexing, integer arithmetic only.
    /// Result is returned as an integer scaled by 10 (12.3 -> 123).
    /// StationStats' int-based Min/Max/Sum fields consume it directly.
    /// </summary>
    [Benchmark]
    public unsafe int IntParseBranchless()
    {
        fixed (byte* ptr = _bytes)
            return ParseTemperatureInt(ptr, _bytes.Length);
    }

    /// <summary>
    /// Total cost when the Level5 result needs to be converted to double.
    /// Shows the real cost for code that requires a double output.
    /// </summary>
    [Benchmark]
    public unsafe double IntParseWithDivision()
    {
        fixed (byte* ptr = _bytes)
            return ParseTemperatureInt(ptr, _bytes.Length) / 10.0;
    }

    // -------------------------------------------------------------------------
    // Level4_SharedMemory\Program.cs -- double-based implementation
    // -------------------------------------------------------------------------
    private static double ParseTemperatureDouble(ReadOnlySpan<byte> span)
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

        double result      = 0;
        var decimalFound   = false;
        var decimalPlace   = 0.1;

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

    // -------------------------------------------------------------------------
    // Level5_Simd\Program.cs -- int-based branchless implementation
    // -------------------------------------------------------------------------
    private static unsafe int ParseTemperatureInt(byte* ptr, int len)
    {
        var sign = 1;

        if (ptr[0] == '-')
        {
            sign = -1;
            ptr++;
            len--;
        }

        int value;

        if (len == 3)
        {
            // "D.D" -> D*10 + D   (e.g. "9.1" -> 91)
            value = (ptr[0] - '0') * 10
                  + (ptr[2] - '0');
        }
        else
        {
            // 32.5 => 325
            // "DD.D" -> D*100 + D*10 + D   (e.g. "32.4" -> 324)
            value = (ptr[0] - '0') * 100
                  + (ptr[1] - '0') * 10
                  + (ptr[3] - '0') * 1;
        }

        return sign * value;
    }
}
