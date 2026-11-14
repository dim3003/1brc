using Shared;
using System.Diagnostics;
using System.Globalization;
using System.Text;


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

string[] lines = File.ReadAllLines(GlobalConstants.FilePath, Encoding.UTF8);

var results = lines
    .Select(line => line.Split(';', 2, StringSplitOptions.TrimEntries))
    .Select(parts =>(City: parts[0], Temp: double.Parse(parts[1], CultureInfo.InvariantCulture)))
    .GroupBy(v => v.City)
    .Select(g => new
    {
        City = g.Key,
        Min = g.Min(r => r.Temp),
        Avg = g.Average(r => r.Temp),
        Max = g.Max(r => r.Temp),
    })
    .OrderBy(s => s.City)
    .ToList();

stopwatch.Stop();

var output = "{" + string.Join(", ",
    results.Select(s => $"{s.City}={s.Min:F1}/{s.Avg:F1}/{s.Max:F1}") +
    "}");

Console.WriteLine(output);

Console.WriteLine($"Processed {lines.Length:N0} rows");
Console.WriteLine($"Found {results.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level01_Naive",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: lines.Length,
    stationCount: results.Count);
