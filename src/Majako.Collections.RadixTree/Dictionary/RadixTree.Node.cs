namespace Majako.Collections.RadixTree;

public partial class RadixTree<TValue>
{
    /// <summary>
    /// An implementation of a trie node
    /// </summary>
    protected class Node : BaseNode
    {
        protected ValueWrapper _value;

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
            var wrapper = Value;
            value = default;

            if (wrapper == null)
                return false;

            value = wrapper.Value;

            return true;
        }

        public override bool TryRemoveValue(out TValue value)
        {
            value = default;
            if (Value == null)
                return false;
            value = Value.Value;
            Value = null;
            return true;
        }

        public override TValue GetOrAddValue(TValue value)
        {
            if (Value != null)
                return Value.Value;

            SetValue(value);

            return value;
        }
    }
}
