namespace Majako.Collections.RadixTree.Concurrent;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public partial class ConcurrentRadixTree : PrefixTree
{
    #region Fields

    protected volatile BaseNode _root = new Node();
    protected readonly StripedReaderWriterLock _locks = new();
    protected readonly ReaderWriterLockSlim _structureLock = new();

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new empty instance of <see cref="ConcurrentRadixTree{TValue}" />
    /// </summary>
    public ConcurrentRadixTree()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrentRadixTree" /> with the given items
    /// </summary>
    /// <param name="items">The items to be added to the trie</param>
    public ConcurrentRadixTree(IEnumerable<string> items)
    {
        foreach (var item in items)
            Add(item);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrentRadixTree{TValue}" /> with the given subtree root
    /// </summary>
    /// <param name="subtreeRoot">The root of the subtree</param>
    protected ConcurrentRadixTree(BaseNode subtreeRoot)
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
    public override bool Add(string item)
    {
        ArgumentException.ThrowIfNullOrEmpty(item, nameof(item));

        return GetOrAddNode(item, out _);
    }

    public override bool Contains(string item)
    {
        return Find(item, _root, out _);
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
        IEnumerable<string> traverse(BaseNode n, string s)
        {
            if (n.IsTerminal)
                yield return s;

            var nLock = GetLock(n);
            nLock.EnterReadLock();
            List<BaseNode> children;

            try
            {
                // we can't know what is done during enumeration, so we need to make a copy of the children
                children = [.. n.Children.Values];
            }
            finally
            {
                nLock.ExitReadLock();
            }

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
        return succeeded ? new ConcurrentRadixTree(subtreeRoot) : [];
    }

    #endregion

    /// <summary>
    /// Gets a lock on the node's children
    /// </summary>
    /// <remarks>
    /// May return the same lock for two different nodes, so the user needs to check to avoid lock recursion exceptions
    /// </remarks>
    protected virtual ReaderWriterLockSlim GetLock(BaseNode node)
    {
        return _locks.GetLock(node.Children);
    }

    protected virtual bool Find(string key, BaseNode subtreeRoot, out BaseNode node)
    {
        node = subtreeRoot;

        if (key.Length == 0)
            return true;

        var suffix = key.AsSpan();

        while (true)
        {
            var nodeLock = GetLock(node);
            nodeLock.EnterReadLock();

            try
            {
                if (!node.Children.TryGetValue(suffix[0], out node))
                    return false;
            }
            finally
            {
                nodeLock.ExitReadLock();
            }

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
        ReaderWriterLockSlim nodeLock;
        char c;
        BaseNode nextNode;
        _structureLock.EnterReadLock();

        try
        {
            while (true)
            {
                c = suffix[0];
                nodeLock = GetLock(node);
                nodeLock.EnterUpgradeableReadLock();

                try
                {
                    if (node.Children.TryGetValue(c, out nextNode))
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

                        // we need to add a node, but don't want to hold an upgradeable read lock on _structureLock
                        // since only one can be held at a time, so we break, release the lock and reacquire a write lock
                        break;
                    }

                    // if there is no child starting with c, we can just add and return one
                    nodeLock.EnterWriteLock();

                    try
                    {
                        var suffixNode = new Node(suffix)
                        {
                            IsTerminal = true
                        };

                        node = node.Children[c] = suffixNode;
                        return true;
                    }
                    finally
                    {
                        nodeLock.ExitWriteLock();
                    }
                }
                finally
                {
                    nodeLock.ExitUpgradeableReadLock();
                }
            }
        }
        finally
        {
            _structureLock.ExitReadLock();
        }

        // if we need to restructure the tree, we do it after releasing and reacquiring the lock.
        // however, another thread may have restructured around the node we're on in the meantime,
        // and in that case we need to retry the insertion
        _structureLock.EnterWriteLock();
        nodeLock.EnterUpgradeableReadLock();

        try
        {
            // we use while instead of if so we can break
            while (node != null && !node.IsDeleted && node.Children.TryGetValue(c, out nextNode))
            {
                var label = nextNode.Label.AsSpan();
                var i = GetCommonPrefixLength(label, suffix);

                // suffix starts with label?
                if (i == label.Length)
                {
                    // if the keys are equal, the key has already been inserted
                    if (i == suffix.Length)
                    {
                        node = nextNode;
                        return true;
                    }

                    // structure has changed since last; try again
                    break;
                }

                var splitNode = new Node(suffix[..i])
                {
                    Children = { [label[i]] = new Node(label[i..], nextNode) }
                };

                BaseNode outNode;

                // label starts with suffix, so we can return splitNode
                if (i == suffix.Length)
                    outNode = splitNode;
                // the keys diverge, so we need to branch from splitNode
                else
                    splitNode.Children[suffix[i]] = outNode = new Node(suffix[i..]);

                outNode.IsTerminal = true;
                nodeLock.EnterWriteLock();

                try
                {
                    node.Children[c] = splitNode;
                }
                finally
                {
                    nodeLock.ExitWriteLock();
                }

                node = outNode;
                return true;
            }
        }
        finally
        {
            nodeLock.ExitUpgradeableReadLock();
            _structureLock.ExitWriteLock();
        }

        // we failed to add a node, so we have to retry;
        // the recursive call is placed at the end to enable tail-recursion optimization
        return GetOrAddNode(key, out node);
    }

    /// <summary>
    /// Removes a node from the trie, if found
    /// </summary>
    /// <param name="subtreeRoot">The root of the subtree from which to remove the node</param>
    /// <param name="item">The item to remove</param>
    /// <returns></returns>
    protected virtual bool Remove(BaseNode subtreeRoot, ReadOnlySpan<char> item)
    {
        BaseNode node = null, grandparent = null;
        var parent = subtreeRoot;
        var i = 0;
        _structureLock.EnterReadLock();
        try
        {
            while (i < item.Length)
            {
                var c = item[i];
                var parentLock = GetLock(parent);
                parentLock.EnterReadLock();

                try
                {
                    if (!parent.Children.TryGetValue(c, out node))
                        return false;
                }
                finally
                {
                    parentLock.ExitReadLock();
                }

                var label = node.Label.AsSpan();
                var k = GetCommonPrefixLength(item[i..], label);

                // is this the node we're looking for?
                if (k == label.Length && k == item.Length - i)
                {
                    // this node has to be removed or merged
                    if (node.IsTerminal)
                        break;

                    // the node is either already removed, or it is a branching node
                    return false;
                }

                if (k < label.Length)
                    return false;

                i += label.Length;
                grandparent = parent;
                parent = node;
            }
        }
        finally
        {
            _structureLock.ExitReadLock();
        }

        // if we need to delete a node, the tree has to be restructured to remove empty leaves or merge
        // single children with branching node parents, and other threads may be currently on these nodes
        _structureLock.EnterWriteLock();

        try
        {
            var nodeLock = GetLock(node);
            var parentLock = GetLock(parent);
            var grandparentLock = grandparent != null ? GetLock(grandparent) : null;
            var lockAlreadyHeld = nodeLock == parentLock || nodeLock == grandparentLock;

            if (lockAlreadyHeld)
                nodeLock.EnterUpgradeableReadLock();
            else
                nodeLock.EnterReadLock();

            try
            {
                var c = node.Label[0];
                var nChildren = node.Children.Count;

                // if the node has no children, we can just remove it
                if (nChildren == 0)
                {
                    parentLock.EnterWriteLock();
                    try
                    {
                        // was removed or replaced by another thread
                        if (!parent.Children.TryGetValue(c, out var n) || n != node)
                            return false;

                        parent.Children.Remove(c);
                        node.Delete();

                        // since we removed a node, we may be able to merge a lone sibling with the parent
                        if (parent.Children.Count == 1 && grandparent != null && !parent.IsTerminal)
                        {
                            var grandparentLockAlreadyHeld = grandparentLock == parentLock;

                            if (!grandparentLockAlreadyHeld)
                                grandparentLock.EnterWriteLock();

                            try
                            {
                                c = parent.Label[0];

                                if (!grandparent.Children.TryGetValue(c, out n) || n != parent || parent.IsTerminal)
                                    return false;

                                var child = parent.Children.First().Value;
                                grandparent.Children[c] = new Node(parent.Label + child.Label, child);
                                parent.Delete();
                            }
                            finally
                            {
                                if (!grandparentLockAlreadyHeld)
                                    grandparentLock.ExitWriteLock();
                            }
                        }
                    }
                    finally
                    {
                        parentLock.ExitWriteLock();
                    }
                }
                // if there is a single child, we can merge it with node
                else if (nChildren == 1)
                {
                    parentLock.EnterWriteLock();

                    try
                    {
                        // was removed or replaced by another thread
                        if (!parent.Children.TryGetValue(c, out var n) || n != node)
                            return false;

                        var child = node.Children.FirstOrDefault().Value;
                        parent.Children[c] = new Node(node.Label + child.Label, child);
                        node.Delete();
                    }
                    finally
                    {
                        parentLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                if (lockAlreadyHeld)
                    nodeLock.ExitUpgradeableReadLock();
                else
                    nodeLock.ExitReadLock();
            }
        }
        finally
        {
            _structureLock.ExitWriteLock();
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
            var parentLock = GetLock(parent);
            parentLock.EnterUpgradeableReadLock();

            try
            {
                if (!parent.Children.TryGetValue(c, out node))
                    return false;

                var label = node.Label.AsSpan();
                var k = GetCommonPrefixLength(span[i..], label);

                if (k == span.Length - i)
                {
                    subtreeRoot = new Node(prefix[..i] + node.Label, node);
                    if (!prune)
                        return true;

                    parentLock.EnterWriteLock();

                    try
                    {
                        if (parent.Children.Remove(c, out node))
                            return true;
                    }
                    finally
                    {
                        parentLock.ExitWriteLock();
                    }

                    // was removed by another thread
                    return false;
                }

                if (k < label.Length)
                    return false;

                i += label.Length;
            }
            finally
            {
                parentLock.ExitUpgradeableReadLock();
            }

            parent = node;
        }

        return false;
    }

    #endregion
}
