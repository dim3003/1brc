using Shared;
using System.Diagnostics;
using System.Text;

Console.WriteLine("=== Level 3: Parallel Implementation ===");
Console.WriteLine($"File: {GlobalConstants.FilePath}");
Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
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


var threadCount = Environment.ProcessorCount; // 32
long[] chunkBoundaries = ComputeChunkBoundaries(GlobalConstants.FilePath, threadCount);

var threadLocalResults = new Dictionary<string, StationStats>[threadCount];
var lineCounters = new long[threadCount];

Parallel.For(0, threadCount, threadIndex =>
{
    // by each thread
    var localLineCounter = 0;
    var startByte = chunkBoundaries[threadIndex];
    var endByte = chunkBoundaries[threadIndex + 1];
    var chunkLength = (int)(endByte - startByte);

    // allocation
    var localStats = new Dictionary<string, StationStats>(GlobalConstants.ExpectedStationCount);
    
    var buffer = new byte[chunkLength]; // HUGE allocation

    using (var stream = new FileStream(GlobalConstants.FilePath,
                                       FileMode.Open,
                                       FileAccess.Read,
                                       FileShare.Read))
    {
        stream.Seek(startByte, SeekOrigin.Begin);
        stream.ReadExactly(buffer, 0, chunkLength);
    }

    // another HUGE allocation
    var chunkText = Encoding.UTF8.GetString(buffer);

    var lineStart = 0;

    for (var i = 0; i < chunkText.Length; i++)
    {
        // Processing each line from a bigger string that contains multiple lines.
        if (chunkText[i] == '\n')
        {
            var lineEnd = i;
            if (lineEnd > lineStart && chunkText[lineEnd - 1] == '\r')
                lineEnd--;

            if (lineEnd > lineStart)
            {
                // process file
                ProcessLine(chunkText.AsSpan(lineStart, lineEnd - lineStart), localStats);
                localLineCounter++;
            }

            lineStart = i + 1;
        }
    }

    threadLocalResults[threadIndex] = localStats;
    lineCounters[threadIndex] = localLineCounter;

});


// MERGE Dictionary

var finalResults = new Dictionary<string, StationStats>(GlobalConstants.ExpectedStationCount);

foreach (var localStats in threadLocalResults)
{
    if (localStats == null)
        continue;

    foreach (var (stationName, stats) in localStats)
    {
        if (!finalResults.TryGetValue(stationName, out var existingStats))
        {
            existingStats = new StationStats();
            finalResults[stationName] = existingStats;
        }

        existingStats.Merge(stats);
    }
}

var totalLines = lineCounters.Sum();
stopwatch.Stop();



// =============================================================================
// OUTPUT: Format results as required by 1BRC
// =============================================================================

var sortedResults = finalResults.OrderBy(kvp => kvp.Key).ToList();

var output = ResultLogger.FormatOutput(sortedResults);
Console.WriteLine(output);

Console.WriteLine();
Console.WriteLine($"Processed {totalLines:N0} rows using {threadCount} threads");
Console.WriteLine($"Found {finalResults.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

// Save results to file
ResultLogger.SaveResult(
    projectName: "Level03_Parallel",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: totalLines,
    stationCount: finalResults.Count);

// Per-thread statistics
Console.WriteLine();
Console.WriteLine("Per-Thread Statistics:");
for (var i = 0; i < threadCount; i++)
{
    var stationCount = threadLocalResults[i]?.Count ?? 0;
    Console.WriteLine($"  Thread {i}: {lineCounters[i]:N0} lines, {stationCount} stations");
}





static void ProcessLine(ReadOnlySpan<char> line, Dictionary<string, StationStats> stats)
{
    var separationIndex = line.IndexOf(';');
    if (separationIndex < 0)
        return;

    var stationName = line[..separationIndex].ToString(); // new allocation
    var temp = double.Parse(line[(separationIndex + 1)..]);

    if (!stats.TryGetValue(stationName, out var stationStats))
    {
        stationStats = new StationStats();
        stats[stationName] = stationStats;
    }

    stationStats.Update(temp);
}

static long[] ComputeChunkBoundaries(string filePath, int threadCount)
{
    var fileInfo = new FileInfo(filePath);
    var fileSize = fileInfo.Length;

    var boundaries = new long[threadCount + 1];
    int bomSize = 0; // we may want to hande BOM

    Span<byte> bom = stackalloc byte[4];
    using (var bomStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        int bytesRead = bomStream.Read(bom);
        if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            bomSize = 3; // UTF-8
        else if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            bomSize = 2; // UTF-16 LE
        else if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            bomSize = 2; // UTF-16 BE
        else if (bytesRead >= 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
            bomSize = 4; // UTF-32 LE
    }


    boundaries[threadCount] = fileSize;
    boundaries[0] = bomSize;

    var dataSize = fileSize - bomSize;
    var chunkSize = dataSize / threadCount;


    using var stream = new FileStream(filePath,
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);

    for (int i = 1; i < threadCount; i++)
    {
        // Target end byte position for each thread
        var targetPos = i * chunkSize;

        // See to he target byte position
        stream.Seek(targetPos, SeekOrigin.Begin);

        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\n')
            {
                boundaries[i] = stream.Position;
                break;
            }
        }

        if (b == -1)
        {
            boundaries[i] = fileSize;
        }
    }

    return boundaries;
}