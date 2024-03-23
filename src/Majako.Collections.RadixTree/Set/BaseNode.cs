namespace Majako.Collections.RadixTree;

public abstract partial class PrefixTree
{
    protected abstract class BaseNode(string label = "")
    {
        public virtual IDictionary<char, BaseNode> Children { get; } = new Dictionary<char, BaseNode>();

        public virtual string Label { get; } = label;

        public BaseNode(ReadOnlySpan<char> label) : this(label.ToString())
        {
        }

        public BaseNode(string label, BaseNode node) : this(label)
        {
            Children = node.Children;
            IsTerminal = node.IsTerminal;
        }

        public BaseNode(ReadOnlySpan<char> label, BaseNode node) : this(label.ToString(), node)
        {
        }

        public abstract bool IsTerminal { get; set; }
    }
}
