namespace Majako.Collections.RadixTree;

public abstract partial class PrefixTree<TValue>
{
    protected abstract class BaseNode(string label = "")
    {
        // used to avoid keeping a separate boolean flag that would use another byte per node
        protected static readonly ValueWrapper _deleted = new(default);
        protected abstract ValueWrapper Value { get; set; }

        public BaseNode(ReadOnlySpan<char> label) : this(label.ToString())
        {
        }

        public BaseNode(string label, BaseNode node) : this(label)
        {
            Children = node.Children;
            Value = node.Value;
        }

        public BaseNode(ReadOnlySpan<char> label, BaseNode node) : this(label)
        {
            Children = node.Children;
            Value = node.Value;
        }

        /// <summary>
        /// Attempts to get the node value
        /// </summary>
        /// <param name="value">The node value, if value exists</param>
        /// <returns>
        /// True if value exists, otherwise false
        /// </returns>
        public abstract bool TryGetValue(out TValue value);

        public abstract bool TryRemoveValue(out TValue value);

        public abstract TValue GetOrAddValue(TValue value);

        public virtual IDictionary<char, BaseNode> Children { get; } = new Dictionary<char, BaseNode>();

        public virtual void SetValue(TValue value)
        {
            Value = new ValueWrapper(value);
        }

        public virtual void Delete()
        {
            Value = _deleted;
        }

        public virtual string Label { get; } = label;

        public virtual bool IsDeleted => Value == _deleted;

        public virtual bool HasValue => Value != null && !IsDeleted;
    }
}
