using BenchmarkDotNet.Running;
using Benchmarks.Level4_SharedMemory;


// 1 / 3 => 0.3333333333333334
double x = 0.1;
double y = 0.2;
double z = x + y;
double.Parse("3.1");

Console.WriteLine(z); // 0.3 ???











// Run hash performance analysis (simulates 100M measurements)
//HashDistributionAnalyzer.AnalyzeHashPerformance(100_000_000);

// Or run benchmark
//BenchmarkRunner.Run<TemperatureParsingBenchmark>();
