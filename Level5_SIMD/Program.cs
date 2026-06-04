using Shared;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;



Console.WriteLine("=== Level 5: SIMD (AVX2) Implementation ===");
Console.WriteLine($"File: {GlobalConstants.FilePath}");
Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
Console.WriteLine($"AVX2 Supported: {Avx2.IsSupported}"); // 32 bytes
Console.WriteLine($"AVX-512 Supported: {Avx512F.IsSupported}"); // 64 bytes
Console.WriteLine();

if (!File.Exists(GlobalConstants.FilePath))
{
    Console.WriteLine($"ERROR: File not found at {GlobalConstants.FilePath}");
    return;
}

if (!Avx2.IsSupported)
{
    Console.WriteLine("WARNING: AVX2 not supported. Performance will be limited.");
}

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var stopwatch = Stopwatch.StartNew();

// =============================================================================
// CORE IMPLEMENTATION: Fully optimized SIMD processing
// =============================================================================

var fileInfo = new FileInfo(GlobalConstants.FilePath);
var fileSize = fileInfo.Length;

if (fileSize == 0)
{
    Console.WriteLine("File is empty.");
    return;
}

var threadCount = Environment.ProcessorCount;
var threadLocalResults = new FastHashTable[threadCount];
var lineCounters = new long[threadCount];

using var mmf = MemoryMappedFile.CreateFromFile(GlobalConstants.FilePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
using var accessor = mmf.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read);

unsafe
{
    byte* basePtr = null;
    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);

    try
    {
        // Skip UTF-8 BOM if present
        long dataStart = 0;
        if (fileSize >= 3 && basePtr[0] == 0xEF && basePtr[1] == 0xBB && basePtr[2] == 0xBF)
        {
            dataStart = 3;
        }

        var dataSize = fileSize - dataStart;
        var chunkSize = dataSize / threadCount;

        Parallel.For(0, threadCount, threadIndex =>
        {
            var startPos = dataStart + (threadIndex * chunkSize);
            var endPos = (threadIndex == threadCount - 1) ? fileSize : dataStart + ((threadIndex + 1) * chunkSize);

            // Align to line boundaries
            if (startPos > dataStart)
            {
                while (startPos < fileSize && basePtr[startPos - 1] != '\n')
                    startPos++;
            }

            if (endPos < fileSize && threadIndex < threadCount - 1)
            {
                while (endPos < fileSize && basePtr[endPos - 1] != '\n')
                    endPos++;
            }

            var localTable = new FastHashTable(
                expectedCount: GlobalConstants.ExpectedStationCount,
                allowResize: true);  // Enable dynamic growth by default

            long localLineCount = 0;
            var pos = startPos;


            // SIMD vectors for delimiter search
            Vector256<byte> semicolonVec = Vector256.Create((byte)';');
            Vector256<byte> newlineVec = Vector256.Create((byte)'\n');

            while (pos < endPos)
            {
                var lineStart = pos;

                // Find semicolon using SIMD
                var semicolonPos = FindByteFast(basePtr, pos, endPos, semicolonVec, (byte)';');
                if (semicolonPos >= endPos)
                    break;

                // Find newline using SIMD
                var newlinePos = FindByteFast(basePtr, semicolonPos + 1, endPos, newlineVec, (byte)'\n');
                if (newlinePos >= endPos)
                    break;


                // Station name: [lineStart, semicolonPos)
                var nameLen = (int)(semicolonPos - lineStart);
                var namePtr = basePtr + lineStart;

                // Temperature: [semicolonPos+1, newlinePos) - handle \r\n
                var tempPtr = basePtr + semicolonPos + 1;
                var tempLen = (int)(newlinePos - semicolonPos - 1);
                if (tempLen > 0 && basePtr[newlinePos - 1] == '\r')
                    tempLen--;

                // Parse temperature as integer (branchless) - scaled by 10
                var temperature = ParseTemperatureBranchless(tempPtr, tempLen);

                // Update hash table (zero-copy)
                localTable.AddOrUpdate(namePtr, nameLen, temperature);
                localLineCount++;

                pos = newlinePos + 1;

            }

            threadLocalResults[threadIndex] = localTable;
            lineCounters[threadIndex] = localLineCount;
        });
    }
    finally
    {
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
    }
}


var finalResults = new Dictionary<string, (int Min, int Max, long Sum, long Count)>(GlobalConstants.ExpectedStationCount);

foreach (var localTable in threadLocalResults)
{
    if (localTable == null) continue;

    foreach (var entry in localTable.GetEntries())
    {
        var name = entry.StationName;  // ← Use cached string (created only once)

        if (finalResults.TryGetValue(name, out var existing))
        {
            finalResults[name] = (
                Math.Min(existing.Min, entry.Min),
                Math.Max(existing.Max, entry.Max),
                existing.Sum + entry.Sum,
                existing.Count + entry.Count
            );
        }
        else
        {
            finalResults[name] = (entry.Min, entry.Max, entry.Sum, entry.Count);
        }
    }
}

var totalLines = lineCounters.Sum();
stopwatch.Stop();

// =============================================================================
// OUTPUT: Convert integer temperatures back to doubles
// =============================================================================

var sortedResults = finalResults
    .OrderBy(kvp => kvp.Key)
    .Select(kvp => new KeyValuePair<string, string>(
        kvp.Key,
        $"{kvp.Value.Min / 10.0:F1}/{(kvp.Value.Sum / 10.0) / kvp.Value.Count:F1}/{kvp.Value.Max / 10.0:F1}"
    ))
    .ToList();

var output = "{" + string.Join(", ", sortedResults.Select(kvp => $"{kvp.Key}={kvp.Value}")) + "}";
Console.WriteLine(output);

Console.WriteLine();
Console.WriteLine($"Processed {totalLines:N0} rows using {threadCount} threads");
Console.WriteLine($"Found {finalResults.Count} unique stations");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");

ResultLogger.SaveResult(
    projectName: "Level05_Simd",
    output: output,
    elapsed: stopwatch.Elapsed,
    rowCount: totalLines,
    stationCount: finalResults.Count);

Console.WriteLine();
Console.WriteLine("Memory Statistics:");
Console.WriteLine($"  Working Set: {Environment.WorkingSet / 1024 / 1024:N0} MB");
Console.WriteLine($"  GC Total Memory: {GC.GetTotalMemory(false) / 1024 / 1024:N0} MB");
Console.WriteLine($"  Gen0 Collections: {GC.CollectionCount(0)}");
Console.WriteLine($"  Gen1 Collections: {GC.CollectionCount(1)}");
Console.WriteLine($"  Gen2 Collections: {GC.CollectionCount(2)}");






[MethodImpl(MethodImplOptions.AggressiveInlining)]
static unsafe long FindByteFast(byte* basePtr,
                                long start,
                                long end,
                                Vector256<byte> targetVec,
                                byte target)
{
    var pos = start;

    if (Avx.IsSupported)
    {
        while (pos + 32 <= end)
        {
            var data = Avx.LoadVector256(basePtr + pos);
            var cmp = Avx2.CompareEqual(data, targetVec);
            var mask = (uint)Avx2.MoveMask(cmp);
            
            // 0000000001000000
            if (mask != 0)
                return pos + BitOperations.TrailingZeroCount(mask);

            pos += 32;
        }
    }

    while (pos < end)
    {
        if (basePtr[pos] == target)
            return pos;

        pos++;
    }

    return end;
}





[MethodImpl(MethodImplOptions.AggressiveInlining)]
static unsafe int ParseTemperatureBranchless(byte* ptr, int len)
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
        // 1.2 -> 12
        value = ((ptr[0] - '0') * 10) + (ptr[2] - '0');
    }
    else
    {
        // 12.3 -> 123
        value = ((ptr[0] - '0') * 100)
              + ((ptr[1] - '0') * 10)
              + (ptr[3] - '0');
    }

    // Branchless
    return sign * value;
}

internal unsafe class FastHashTable
{
    private const int DefaultCapacity = 1024;
    private const int MaxCapacity = 32768;
    private const double LoadFactorThreshold = 0.75;

    private Entry[] _entries;
    private int _count;
    private readonly bool _allowResize;

    /// <summary>
    /// Creates a hash table with dynamic or fixed capacity strategy.
    /// </summary>
    /// <param name="expectedCount">Expected number of unique stations (default: 500)</param>
    /// <param name="allowResize">Enable dynamic growth when load factor exceeds threshold (default: true)</param>
    public FastHashTable(int expectedCount = 500, bool allowResize = true)
    {
        _allowResize = allowResize;

        // Calculate initial capacity
        var targetCapacity = (int)(expectedCount / LoadFactorThreshold);
        var capacity = NextPowerOf2(Math.Max(DefaultCapacity, targetCapacity));
        capacity = Math.Min(capacity, MaxCapacity);

        _entries = new Entry[capacity];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrUpdate(byte* namePtr, int nameLen, int temperature)
    {
        // Check if resize is needed before adding
        if (_allowResize && _count >= _entries.Length * LoadFactorThreshold)
        {
            var newCapacity = Math.Min(_entries.Length * 2, MaxCapacity);
            if (newCapacity > _entries.Length)
            {
                Resize(newCapacity);
            }
        }

        var hash = ComputeHash(namePtr, nameLen);
        var index = (int)(hash & (uint)(_entries.Length - 1)); // 1 cycle
        // 143 % 10 => [3] // ~3-4 cycle


        while (true)
        {
            ref Entry entry = ref _entries[index];

            if (entry.Name is null) // first visit
            {
                // New entry - copy name bytes AND create string once
                entry.Name = new byte[nameLen];

                fixed (byte* dest = entry.Name)
                {
                    Buffer.MemoryCopy(namePtr, dest, nameLen, nameLen);
                }

                entry.StationName = Encoding.UTF8.GetString(entry.Name);  // ← Create string ONCE
                entry.Hash = hash;
                entry.Min = temperature;
                entry.Max = temperature;
                entry.Sum = temperature;
                entry.Count = 1;
                _count++;

                return;
            }


            // not the first visit
            if (entry.Hash == hash && entry.Name.Length == nameLen)
            {
                // Verify bytes match
                fixed (byte* entryName = entry.Name)
                {
                    var match = true;
                    for (var i = 0; i < nameLen; i++)
                    {
                        // Paris - Paris
                        if (entryName[i] != namePtr[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        // Update statistics
                        //if (temperature < entry.Min) entry.Min = temperature;
                        //if (temperature > entry.Max) entry.Max = temperature;

                        // less branching???
                        // source.dot.net
                        entry.Min = Math.Min(temperature, entry.Min);
                        entry.Max = Math.Max(temperature, entry.Max);

                        entry.Sum += temperature;
                        entry.Count++;
                        return;
                    }
                }
            }


            // Linear probing
            index = (index + 1) & (_entries.Length - 1);



        }
    }







    private void Resize(int newCapacity)
    {
        var oldEntries = _entries;
        _entries = new Entry[newCapacity];
        _count = 0;

        // Rehash all existing entries
        foreach (var oldEntry in oldEntries)
        {
            if (oldEntry.Name != null)
            {
                fixed (byte* ptr = oldEntry.Name)
                {
                    // Reinsert into new table
                    var hash = oldEntry.Hash;

                    // branchless - 1 cycle
                    var index = (int)(hash & (uint)(_entries.Length - 1));
                    // 209 % 10 => ~3-4 cycle

                    while (true)
                    {
                        ref Entry entry = ref _entries[index];

                        if (entry.Name is null)
                        {
                            // Found empty slot - copy data
                            entry = oldEntry;
                            _count++;
                            break;
                        }

                        // Linear probing
                        index = (index + 1) & (_entries.Length - 1);
                    }
                }
            }
        }
    }


    public IEnumerable<Entry> GetEntries()
    {
        foreach (var entry in _entries)
        {
            if (entry.Name != null)
                yield return entry;
        }
    }







    public struct Entry
    {
        public byte[] Name;       // Raw UTF-8 bytes (for comparison)
        public string StationName; // Cached string (created once)
        public uint Hash;
        public int Min;           // Temperature * 10
        public int Max;           // Temperature * 10
        public long Sum;          // Sum of temperatures * 10
        public long Count;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeHash(byte* ptr, int len)
    {
        // FNV-1a hash - good distribution, fast
        var hash = 2166136261u;
        for (var i = 0; i < len; i++)
        {
            hash ^= ptr[i];
            hash *= 16777619u;
        }

        return hash;
    }


    // Branchless
    private static int NextPowerOf2(int n)
    {
        if (n <= 0)
            return 1;

        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;

        return n + 1;
    }
}