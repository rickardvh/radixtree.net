namespace Majako.Collections.RadixTree.Concurrent;

public partial class ConcurrentRadixTree
{
    protected class Node : BaseNode
    {
        protected volatile bool _isTerminal;

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
