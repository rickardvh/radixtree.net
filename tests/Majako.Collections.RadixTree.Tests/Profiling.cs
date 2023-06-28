using System.Diagnostics;
using Xunit.Abstractions;

namespace Majako.Collections.RadixTree.Tests
{
    public class Profiling
    {
        private readonly ITestOutputHelper _output;
        private readonly ITree<int> _sut = new ConcurrentTrie<int>();

        public Profiling(ITestOutputHelper output)
        {
            _output = output;
        }

        private void ProfileAction(Action action)
        {
            var sw = new Stopwatch();
            var memory = GC.GetTotalMemory(true) >> 20;
            sw.Start();

            action.Invoke();

            sw.Stop();
            var delta = (GC.GetTotalMemory(true) >> 20) - memory;
            _output.WriteLine("Elapsed time: {0:F}s", sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine("Memory usage: {0:F}Mb", delta);
        }

        [Fact]
        public void Profile()
        {
            ProfileAction(() =>
            {
                for (var i = 0; i < 1000000; i++)
                    _sut.Add(Guid.NewGuid().ToString(), 0);
            });
        }

        [Fact]
        public void ProfilePrune()
        {
            // insert
            for (var i = 0; i < 10000; i++)
                _sut.Add(Guid.NewGuid().ToString(), 0);

            ProfileAction(() =>
            {
                Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, j =>
                {
                    for (var i = 0; i < 20; i++)
                    {
                        // insert
                        _sut.Add(Guid.NewGuid().ToString(), 0);

                        // remove by prefix
                        _sut.Prune(Guid.NewGuid().ToString()[..5], out _);
                    }
                });
            });
        }
    }
}
