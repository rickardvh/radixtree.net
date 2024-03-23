namespace Majako.Collections.RadixTree;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public partial class RadixTree : PrefixTree
{
    #region Fields

    protected BaseNode _root = new Node();

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new empty instance of <see cref="RadixTree" />
    /// </summary>
    public RadixTree()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RadixTree" /> with the given items
    /// </summary>
    /// <param name="items">The items to be added to the trie</param>
    public RadixTree(IEnumerable<string> items)
    {
        foreach (var item in items)
            Add(item);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RadixTree" /> with the given subtree root
    /// </summary>
    /// <param name="subtreeRoot">The root of the subtree</param>
    protected RadixTree(BaseNode subtreeRoot)
    {
        if (subtreeRoot.Label.Length == 0)
            _root = subtreeRoot;
        else
            _root.Children[subtreeRoot.Label[0]] = subtreeRoot;
    }

    #endregion

    #region Methods

    #region Public

    /// <inheritdoc/>
    public override bool Add(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        return GetOrAddNode(key, out _);
    }

    public override bool Contains(string item)
    {
        return Find(item, _root, out _);
    }

    public override void SymmetricExceptWith(IEnumerable<string> other)
    {
        var set = new HashSet<string>(other);
        var toAdd = new List<string>();
        var toRemove = new List<string>();

        foreach (var item in this)
            (set.Contains(item) ? toRemove : toAdd).Add(item);

        ExceptWith(toRemove);
        UnionWith(toAdd);
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        _root = new Node();
    }

    /// <inheritdoc/>
    public override bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        return Remove(_root, key);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> Search(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (!SearchOrPrune(prefix, false, out var node))
            return [];

        // depth-first traversal
        static IEnumerable<string> traverse(BaseNode n, string s)
        {
            if (n.IsTerminal)
                yield return s;

            List<BaseNode> children;

            // we can't know what is done during enumeration, so we need to make a copy of the children
            children = [.. n.Children.Values];

            foreach (var child in children)
                foreach (var kv in traverse(child, s + child.Label))
                    yield return kv;
        }

        return traverse(node, node.Label);
    }

    /// <inheritdoc/>
    public override IPrefixTree Prune(string prefix)
    {
        var succeeded = SearchOrPrune(prefix, true, out var subtreeRoot);
        return succeeded ? new RadixTree(subtreeRoot) : [];
    }

    #endregion

    protected virtual bool Find(string key, BaseNode subtreeRoot, out BaseNode node)
    {
        node = subtreeRoot;

        if (key.Length == 0)
            return true;

        var suffix = key.AsSpan();

        while (true)
        {
            if (!node.Children.TryGetValue(suffix[0], out node))
                return false;

            var span = node.Label.AsSpan();
            var i = GetCommonPrefixLength(suffix, span);

            if (i != span.Length)
                return false;

            if (i == suffix.Length)
                return node.IsTerminal;

            suffix = suffix[i..];
        }
    }

    protected virtual bool GetOrAddNode(ReadOnlySpan<char> key, out BaseNode node)
    {
        node = _root;
        var suffix = key;
        char c;

        while (true)
        {
            c = suffix[0];

            if (node.Children.TryGetValue(c, out BaseNode nextNode))
            {
                var label = nextNode.Label.AsSpan();
                var i = GetCommonPrefixLength(label, suffix);
                // suffix starts with label
                if (i == label.Length)
                {
                    // keys are equal - this is the node we're looking for
                    if (i == suffix.Length)
                    {
                        node = nextNode;
                        return true;
                    }

                    // advance the suffix and continue the search from nextNode
                    suffix = suffix[label.Length..];
                    node = nextNode;

                    continue;
                }

                // we need to add a node between node and nextNode
                var splitNode = new Node(suffix[..i])
                {
                    Children = { [label[i]] = new Node(label[i..], nextNode) }
                };

                node.Children[c] = splitNode;

                // label starts with suffix, so we can return splitNode
                if (i == suffix.Length)
                    node = splitNode;
                // the keys diverge, so we need to branch from splitNode
                else
                    splitNode.Children[suffix[i]] = node = new Node(suffix[i..]);
                
                node.IsTerminal = true;
                return true;
            }

            var suffixNode = new Node(suffix)
            {
                IsTerminal = true
            };

            node = node.Children[c] = suffixNode;
            return true;
        }
    }

    /// <summary>
    /// Removes a node from the trie, if found
    /// </summary>
    /// <param name="subtreeRoot">The root of the subtree from which to remove the node</param>
    /// <param name="key">The key to remove</param>
    /// <param name="valueWrapper">(Optional) The value to remove. If specified, the node will only be removed if its value matches the wrapped value</param>
    /// <returns></returns>
    protected virtual bool Remove(BaseNode subtreeRoot, ReadOnlySpan<char> key)
    {
        BaseNode node = null, grandparent = null;
        var parent = subtreeRoot;
        var i = 0;

        while (i < key.Length)
        {
            if (!parent.Children.TryGetValue(key[i], out node))
                return false;

            var label = node.Label.AsSpan();
            var k = GetCommonPrefixLength(key[i..], label);

            // is this the node we're looking for?
            if (k == label.Length && k == key.Length - i)
            {
                // this node has to be removed or merged
                if (node.IsTerminal)
                    break;

                // the node is a branching node
                return false;
            }

            if (k < label.Length)
                return false;

            i += label.Length;
            grandparent = parent;
            parent = node;
        }

        var c = node.Label[0];
        var nChildren = node.Children.Count;

        // if the node has no children, we can just remove it
        if (nChildren == 0)
        {
            parent.Children.Remove(c);

            // since we removed a node, we may be able to merge a lone sibling with the parent
            if (parent.Children.Count == 1 && grandparent != null && !parent.IsTerminal)
            {
                c = parent.Label[0];
                var child = parent.Children.First().Value;
                grandparent.Children[c] = new Node(parent.Label + child.Label, child);
            }
        }
        // if there is a single child, we can merge it with node
        else if (nChildren == 1)
        {
            var child = node.Children.First().Value;
            parent.Children[c] = new Node(node.Label + child.Label, child);
            node.IsTerminal = false;
        }
        return true;
    }

    protected bool SearchOrPrune(string prefix, bool prune, out BaseNode subtreeRoot)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (prefix.Length == 0)
        {
            subtreeRoot = _root;
            if (prune)
                _root = new Node();
            return true;
        }

        subtreeRoot = default;
        var node = _root;
        var parent = node;
        var span = prefix.AsSpan();
        var i = 0;

        while (i < span.Length)
        {
            var c = span[i];

            if (!parent.Children.TryGetValue(c, out node))
                return false;

            var label = node.Label.AsSpan();
            var k = GetCommonPrefixLength(span[i..], label);

            if (k == span.Length - i)
            {
                subtreeRoot = new Node(prefix[..i] + node.Label, node);
                return !prune || parent.Children.Remove(c, out _);
            }

            if (k < label.Length)
                return false;

            i += label.Length;

            parent = node;
        }

        return false;
    }

    #endregion
}
