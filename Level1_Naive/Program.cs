using Microsoft.VisualBasic;
using Shared;
using System.Diagnostics;
using System.Text;

Console.WriteLine("=== Level 1: Naive (LINQ) Implementation ===");
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

// Bring the file content with all the lines // 13.1MB
string[] lines = File.ReadAllLines(GlobalConstants.FilePath, Encoding.UTF8);

var results = lines.Select(line => // iteration cost
{
    // Create an array (new allocation) with [size: 2]
    var parts = line.Split(';');

    var stationName = parts[0]; // no allocation just assignment
    double temperature = double.Parse(parts[1]); // no allocation

    return new // new allocation - object
    {
        Station = stationName,
        Temperature = temperature
    };
})
.GroupBy(x => x.Station) // after this point, we'll have 413 rows, iteration cost
.Select(g => new // new allocation - object
{
    Station = g.Key,
    Min = g.Min(x => x.Temperature), // iteration cost
    Mean = g.Average(x => x.Temperature),
    Max = g.Max(x => x.Temperature)
})
.OrderBy(o => o.Station) // 413 rows to order
.ToList(); // new allocation - List<AnonymousObject>

stopwatch.Stop();

var output = "{" + string.Join(", ",
    results.Select(r => $"{r.Station}={r.Min:F1}/{r.Mean:F1}/{r.Max:F1}")) +
    "}";

Console.WriteLine(output);

Console.WriteLine();
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