using BenchmarkDotNet.Running;

namespace Phenotype.PostFx.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<PostStackBenchmarks>(
            null,
            args);
    }
}
