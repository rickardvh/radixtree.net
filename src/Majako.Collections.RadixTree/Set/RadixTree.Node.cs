namespace Majako.Collections.RadixTree;

public partial class RadixTree
{
    /// <summary>
    /// An implementation of a trie node
    /// </summary>
    protected class Node : BaseNode
    {
        protected bool _isTerminal;

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

        public override bool IsTerminal
        {
            get => _isTerminal;
            set => _isTerminal = value;
        }
    }
}
