namespace Majako.Collections.RadixTree;

public abstract partial class PrefixTree
{
    protected abstract class BaseNode(string label = "")
    {
        // used to avoid keeping a separate boolean flag that would use another byte per node
        protected static readonly IDictionary<char, BaseNode> _deleted = new Dictionary<char, BaseNode>();
        public abstract bool IsTerminal { get; set; }

        public virtual IDictionary<char, BaseNode> Children { get; protected set; } = new Dictionary<char, BaseNode>();
        public virtual string Label { get; } = label;
        public virtual bool IsDeleted => Children == _deleted;

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

        public virtual void Delete()
        {
            Children = _deleted;
        }
    }
}
