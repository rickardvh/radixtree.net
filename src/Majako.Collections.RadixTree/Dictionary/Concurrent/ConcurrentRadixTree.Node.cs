namespace Majako.Collections.RadixTree.Concurrent;

public partial class ConcurrentRadixTree<TValue>
{
    /// <summary>
    /// An implementation of a trie node
    /// </summary>
    protected class Node : BaseNode
    {
        protected volatile ValueWrapper _value;
        protected override ValueWrapper Value { get => _value; set => _value = value; }

        public Node(string label = "") : base(label)
        {
        }

        public Node(ReadOnlySpan<char> label) : base(label)
        {
        }

        public Node(string label, BaseNode node) : base(label, node)
        {
        }

        public Node(ReadOnlySpan<char> label, BaseNode node) : base(label, node)
        {
        }

        /// <summary>
        /// Attempts to get the node value
        /// </summary>
        /// <param name="value">The node value, if value exists</param>
        /// <returns>
        /// True if value exists, otherwise false
        /// </returns>
        public override bool TryGetValue(out TValue value)
        {
            var wrapper = _value;
            value = default;

            if (wrapper == null)
                return false;

            value = wrapper.Value;

            return true;
        }

        public override bool TryRemoveValue(out TValue value)
        {
            var wrapper = Interlocked.Exchange(ref _value, null);
            value = default;

            if (wrapper == null)
                return false;

            value = wrapper.Value;

            return true;
        }

        public override TValue GetOrAddValue(TValue value)
        {
            var wrapper = Interlocked.CompareExchange(ref _value, new ValueWrapper(value), null);

            return wrapper != null ? wrapper.Value : value;
        }
    }
}
