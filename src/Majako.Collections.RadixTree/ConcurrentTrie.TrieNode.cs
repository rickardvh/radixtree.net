namespace Majako.Collections.RadixTree;

public partial class ConcurrentTrie<TValue>
{
    /// <summary>
    /// An implementation of a trie node
    /// </summary>
    protected class TrieNode(string label = "")
    {
        // used to avoid keeping a separate boolean flag that would use another byte per node
        protected static readonly ValueWrapper _deleted = new(default);
        protected volatile ValueWrapper _value;

        public TrieNode(ReadOnlySpan<char> label) : this(label.ToString())
        {
        }

        public TrieNode(string label, TrieNode node) : this(label)
        {
            Children = node.Children;
            _value = node._value;
        }

        public TrieNode(ReadOnlySpan<char> label, TrieNode node) : this(label)
        {
            Children = node.Children;
            _value = node._value;
        }

        /// <summary>
        /// Attempts to get the node value
        /// </summary>
        /// <param name="value">The node value, if value exists</param>
        /// <returns>
        /// True if value exists, otherwise false
        /// </returns>
        public bool TryGetValue(out TValue value)
        {
            var wrapper = _value;
            value = default;

            if (wrapper == null)
                return false;

            value = wrapper.Value;

            return true;
        }

        public bool TryRemoveValue(out TValue value)
        {
            var wrapper = Interlocked.Exchange(ref _value, null);
            value = default;

            if (wrapper == null)
                return false;

            value = wrapper.Value;

            return true;
        }

        public void SetValue(TValue value)
        {
            _value = new ValueWrapper(value);
        }

        public TValue GetOrAddValue(TValue value)
        {
            var wrapper = Interlocked.CompareExchange(ref _value, new ValueWrapper(value), null);

            return wrapper != null ? wrapper.Value : value;
        }

        public void Delete()
        {
            _value = _deleted;
        }

        public Dictionary<char, TrieNode> Children { get; } = [];

        public string Label { get; } = label;

        public bool IsDeleted => _value == _deleted;

        public bool HasValue => _value != null && !IsDeleted;
    }
}
