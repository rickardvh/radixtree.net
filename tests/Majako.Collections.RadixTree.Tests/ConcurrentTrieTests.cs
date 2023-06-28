using FluentAssertions;

namespace Majako.Collections.RadixTree.Tests
{
    public class ConcurrentTrieTests
    {
        private ITree<int> _sut = new ConcurrentTrie<int>();

        [Fact]
        public void CanAddAndGetValue()
        {
            _sut.TryGetValue("a", out _).Should().BeFalse();
            _sut.Add("a", 1);
            _sut.TryGetValue("a", out var value).Should().BeTrue();
            value.Should().Be(1);
            _sut.Add("a", 2);
            _sut.TryGetValue("a", out value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void CanAddAndGetValues()
        {
            _sut.Add("a", 1);
            _sut.TryGetValue("ab", out _).Should().BeFalse();
            _sut.Add("abc", 3);
            _sut.TryGetValue("ab", out _).Should().BeFalse();
            _sut.TryGetValue("a", out var value).Should().BeTrue();
            value.Should().Be(1);
            _sut.TryGetValue("abc", out value).Should().BeTrue();
            value.Should().Be(3);
            _sut.Add("ab", 2);
            _sut.TryGetValue("ab", out value).Should().BeTrue();
            value.Should().Be(2);
        }

        [Fact]
        public void DoesNotBlockWhileEnumerating()
        {
            _sut.Add("a", 0);
            _sut.Add("ab", 0);
            foreach (var item in _sut.Keys)
                _sut.Remove(item);
        }

        [Fact]
        public void CanRemoveValue()
        {
            _sut.Add("a", 1);
            _sut.Add("b", 1);
            _sut.Add("bbb", 1);
            _sut.Add("ab", 1);
            _sut.Add("aa", 1);
            _sut.Add("abc", 1);
            _sut.Add("abb", 1);
            _sut.Remove("ab");
            _sut.TryGetValue("ab", out _).Should().BeFalse();
            _sut.Keys.Should().BeEquivalentTo(new[] { "abc", "a", "b", "aa", "abb", "bbb" });
            _sut.Remove("ab");
            _sut.Remove("bb");
            _sut.TryGetValue("b", out _).Should().BeTrue();
            _sut.TryGetValue("bbb", out _).Should().BeTrue();

            _sut.Prune("b", out _sut);
            _sut.Keys.Should().BeEquivalentTo(new[] { "b", "bbb" });
            _sut.Remove("b");
            _sut.Keys.Should().BeEquivalentTo(new[] { "bbb" });
        }

        [Fact]
        public void CanGetKeys()
        {
            var keys = new[] { "a", "b", "abc" };
            foreach (var key in keys)
                _sut.Add(key, 1);
            _sut.Keys.Should().BeEquivalentTo(keys);
        }

        [Fact]
        public void CanPrune()
        {
            _sut.Add("a", 1);
            _sut.Add("b", 1);
            _sut.Add("bba", 1);
            _sut.Add("bbb", 1);
            _sut.Add("ab", 1);
            _sut.Add("abc", 1);
            _sut.Add("abd", 1);
            _sut.Prune("bc", out _).Should().BeFalse();
            _sut.Prune("ab", out var subtree).Should().BeTrue();
            subtree.Keys.Should().BeEquivalentTo(new[] { "ab", "abc", "abd" });
            _sut.Keys.Should().BeEquivalentTo(new[] { "a", "b", "bba", "bbb" });
            _sut.Prune("b", out subtree).Should().BeTrue();
            subtree.Keys.Should().BeEquivalentTo(new[] { "b", "bba", "bbb" });
            _sut.Keys.Should().BeEquivalentTo(new[] { "a" });

            _sut = subtree;
            _sut.Prune("bb", out subtree).Should().BeTrue();
            subtree.Keys.Should().BeEquivalentTo(new[] { "bba", "bbb" });
            _sut.Keys.Should().BeEquivalentTo(new[] { "b" });
            _sut = subtree;
            _sut.Prune("bba", out subtree);
            subtree.Keys.Should().BeEquivalentTo(new[] { "bba" });
            _sut.Keys.Should().BeEquivalentTo(new[] { "bbb" });

            _sut = new ConcurrentTrie<int>();
            _sut.Add("aaa", 1);
            _sut.Prune("a", out subtree).Should().BeTrue();
            subtree.Keys.Should().BeEquivalentTo(new[] { "aaa" });
            _sut.Keys.Should().BeEmpty();
            _sut = subtree;
            _sut.Prune("aa", out subtree).Should().BeTrue();
            _sut.Keys.Should().BeEmpty();
            subtree.Keys.Should().BeEquivalentTo(new[] { "aaa" });
        }

        [Fact]
        public void CanSearch()
        {
            _sut.Add("a", 1);
            _sut.Add("b", 1);
            _sut.Add("ab", 1);
            _sut.Add("abc", 2);
            var keys = _sut.Keys.ToList();
            _sut.Search("ab").Should().BeEquivalentTo(new KeyValuePair<string, int>[]
            {
                new("ab", 1),
                new("abc", 2)
            });
            _sut.Keys.Should().BeEquivalentTo(keys);
        }

        [Fact]
        public void CanClear()
        {
            _sut.Add("a", 1);
            _sut.Add("ab", 1);
            _sut.Add("abc", 1);
            _sut.Clear();
            _sut.Keys.Should().BeEmpty();
        }

        [Fact]
        public void DoesNotBreakDuringParallelAddRemove()
        {
            Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, j =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    var s = $"{i}-{j}";
                    _sut.Add(s, i);
                    _sut.TryGetValue(s, out var value).Should().BeTrue();
                    value.Should().Be(i);
                    _sut.Remove(s);
                    _sut.TryGetValue(s, out _).Should().BeFalse();
                }
            });

            _sut.Keys.Count().Should().Be(0);
        }

        [Fact]
        public void DoesNotBreakDuringParallelAddPrune()
        {
            Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, j =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    var s = $"{j}-{i}";
                    _sut.Add(s, i);
                }
                _sut.Prune($"{j}-", out var st);
                st.Keys.Count().Should().Be(1000);
            });

            _sut.Keys.Count().Should().Be(0);
        }
    }
}
