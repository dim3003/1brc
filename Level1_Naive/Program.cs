using Shared;
using System.Diagnostics;


Console.WriteLine("≡≡≡ Level 1: Naive (LINQ) Implementation ≡≡≡");
Console.WriteLine($"File: {GlobalConstants.FilePath}");
Console.WriteLine();

// Verify file exists
if (!File.Exists(GlobalConstants.FilePath)
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


stopwatch.Stop();

var output = "";

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
