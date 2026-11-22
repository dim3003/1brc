using Shared;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Text;

var fr = CultureInfo.GetCultureInfo("fr-CH");


Console.WriteLine("≡≡≡ Level 1: Naive (LINQ) Implementation ≡≡≡");
Console.WriteLine($"File: {GlobalConstants.FilePath}");
Console.WriteLine();

// Verify file exists
if (!File.Exists(GlobalConstants.FilePath))
{
    Console.WriteLine($"ERROR: File not found at {GlobalConstants.FilePath}");
    Console.WriteLine("Please create a test file or update GlobalConstants.FilePath");
    return;
}

// Force garbage collection before measurement for accurate timing
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var stopwatch = Stopwatch.StartNew();

var stats = new Dictionary<string, (double Min, double Max, double Sum, long Count)>();

using var reader = new StreamReader(GlobalConstants.FilePath, Encoding.UTF8);
string? line;
while ((line = reader.ReadLine()) != null)
{
    var separatorIndex = line.IndexOf(';');
    var city = line.Substring(0, separatorIndex);
    var temp = double.Parse(line.Substring(separatorIndex + 1), fr);

    if (stats.TryGetValue(city, out var s))
        stats[city] = (Math.Min(s.Min, temp), Math.Max(s.Max, temp), s.Sum + temp, s.Count + 1);
    else
        stats[city] = (temp, temp, temp, 1);
}

var results = stats
    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
    .Select(kv => new
    {
        City = kv.Key,
        Min = kv.Value.Min,
        Avg = kv.Value.Sum / kv.Value.Count,
        Max = kv.Value.Max,
    })
    .ToList();

stopwatch.Stop();

var output = "{" + string.Join(", ",
    results.Select(s => $"{s.City}={s.Min:F1}/{s.Avg:F1}/{s.Max:F1}")) +
    "}";

Console.WriteLine(output);

Console.WriteLine($"Processed {stats.Count:N0} rows");
Console.WriteLine($"Found {results.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level01_Naive",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: stats.Count,
    stationCount: results.Count);
