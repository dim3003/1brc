using Shared;
using System.Diagnostics;
using System.Text;

Console.WriteLine("=== Level 2: Streaming Implementation ===");
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

// one time allocation
using var reader = new StreamReader(GlobalConstants.FilePath,
                                    Encoding.UTF8,
                                    true,
                                    bufferSize: 1024); // 1KB

// HomeWork: Disk Sector Size, Disk Cluster Size, OS Page Size, Buffer Size

string line;
var lineCounter = 0;

// one time allocation
var stations = new Dictionary<string, StationStats>
    (capacity: GlobalConstants.ExpectedStationCount);

// each line allocates new string
while ((line = reader.ReadLine()) is not null)
{
    lineCounter++;
    // line: istanbul;25.4
    var separatorIndex = line.IndexOf(';'); // no allocation

    var stationName = line[..separatorIndex]; // new string allocation
    var temp = double.Parse(line.AsSpan(start: separatorIndex + 1)); // no allocation

    if (!stations.TryGetValue(stationName, out var stat)) // lookup
    {
        stat = new StationStats(); // new allocation for each unique stationName - 413
        stations.Add(stationName, stat);
    }

    stat.Update(temp); // no allocation
}

stopwatch.Stop();


// =============================================================================
// OUTPUT: Format results as required by 1BRC
// =============================================================================

// Sort by station name
var sortedResults = stations.OrderBy(kvp => kvp.Key).ToList(); // tolist allocation

var output = ResultLogger.FormatOutput(sortedResults);
Console.WriteLine(output);

Console.WriteLine();
Console.WriteLine($"Processed {lineCounter:N0} rows");
Console.WriteLine($"Found {stations.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level2_Stream",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: lineCounter,
    stationCount: stations.Count);