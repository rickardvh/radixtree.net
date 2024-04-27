using System.Runtime.CompilerServices;

namespace Majako.Collections.RadixTree.Concurrent;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public partial class ConcurrentRadixTree<TValue> : PrefixTree<TValue>
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
    /// Initializes a new instance of <see cref="ConcurrentRadixTree{TValue}" /> with the given items
    /// </summary>
    /// <param name="items">The items to be added to the trie</param>
    public ConcurrentRadixTree(IEnumerable<KeyValuePair<string, TValue>> items)
    {
        foreach (var (key, value) in items)
            Add(key, value);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrentRadixTree{TValue}" /> with the given subtree root
    /// </summary>
    /// <param name="subtreeRoot">The root of the subtree</param>
    protected ConcurrentRadixTree(BaseNode subtreeRoot)
    {
        if (subtreeRoot.Label == string.Empty)
            _root = subtreeRoot;
        else
            _root.Children[subtreeRoot.Label[0]] = subtreeRoot;
    }

    #endregion

    #region Methods

    #region Public

    /// <inheritdoc/>
    public override bool TryGetValue(string key, out TValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        value = default;

        return Find(key, _root, out var node) && node.TryGetValue(out value);
    }

    /// <inheritdoc/>
    public override void Add(string key, TValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        GetOrAddNode(key, value, true);
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
    public override bool Remove(KeyValuePair<string, TValue> item)
    {
        ArgumentException.ThrowIfNullOrEmpty(item.Key, nameof(item.Key));

        return Remove(_root, item.Key, new ValueWrapper(item.Value));
    }

    /// <inheritdoc/>
    public override IEnumerable<KeyValuePair<string, TValue>> Search(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (!SearchOrPrune(prefix, false, out var node))
            return [];

        // depth-first traversal
        IEnumerable<KeyValuePair<string, TValue>> traverse(BaseNode n, string s)
        {
            if (n.TryGetValue(out var value))
                yield return new KeyValuePair<string, TValue>(s, value);

            IList<BaseNode> children;
            // we can't know what is done during enumeration, so we need to make a copy of the children
            using (new LockWrapper(GetLock(n), LockType.Read))
                children = [.. n.Children.Values];

            foreach (var child in children)
                foreach (var kv in traverse(child, s + child.Label))
                    yield return kv;
        }

        return traverse(node, node.Label);
    }

    /// <inheritdoc/>
    public override IPrefixTree<TValue> Prune(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return SearchOrPrune(prefix, true, out var subtreeRoot) ? new ConcurrentRadixTree<TValue>(subtreeRoot) : [];
    }

    #endregion

    /// <summary>
    /// Gets a lock on the node's children
    /// </summary>
    /// <remarks>
    /// May return the same lock for two different nodes, so the user needs to check to avoid lock recursion exceptions
    /// </remarks>
    protected virtual ReaderWriterLockSlim GetLock(BaseNode node) => _locks.GetLock(node.Children);

    protected virtual bool Find(ReadOnlySpan<char> key, BaseNode subtreeRoot, out BaseNode node)
    {
        node = subtreeRoot;

        if (key.Length == 0)
            return true;

        for (var suffix = key; ;)
        {
            using (new LockWrapper(GetLock(node), LockType.Read))
            {
                if (!node.Children.TryGetValue(suffix[0], out node))
                    return false;
            }

            var span = node.Label.AsSpan();
            var i = GetCommonPrefixLength(suffix, span);

            if (i != span.Length)
                return false;

            if (i == suffix.Length)
                return node.HasValue;

            suffix = suffix[i..];
        }
    }

    protected virtual BaseNode GetOrAddNode(ReadOnlySpan<char> key, TValue value, bool overwrite = false)
    {
        var node = _root;
        var suffix = key;
        ReaderWriterLockSlim nodeLock;
        char c;

        using (new LockWrapper(_structureLock, LockType.Read))
        {
            while (true)
            {
                c = suffix[0];
                nodeLock = GetLock(node);
                using (new LockWrapper(nodeLock, LockType.UpgradeableRead))
                {
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
                                if (overwrite)
                                    nextNode.SetValue(value);
                                else
                                    nextNode.GetOrAddValue(value);

                                return nextNode;
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
                    using (new LockWrapper(nodeLock, LockType.Write))
                    {
                        var suffixNode = new Node(suffix);
                        suffixNode.SetValue(value);

                        return node.Children[c] = suffixNode;
                    }
                }
            }
        }

        // if we need to restructure the tree, we do it after releasing and reacquiring the lock.
        // however, another thread may have restructured around the node we're on in the meantime,
        // and in that case we need to retry the insertion
        if (AddAndRestructure(node, suffix, overwrite, value, out var addedNode))
            return addedNode;

        // we failed to add a node, so we have to retry;
        // the recursive call is placed at the end to enable tail-recursion optimization
        return GetOrAddNode(key, value, overwrite);
    }

    protected bool AddAndRestructure(BaseNode node, ReadOnlySpan<char> suffix, bool overwrite, TValue value, out BaseNode addedNode)
    {
        addedNode = default;

        using var _ = new LockWrapper(_structureLock, LockType.Write);
        var nodeLock = GetLock(node);
        using var _nodeLockWrapper = new LockWrapper(nodeLock, LockType.UpgradeableRead);

        var c = suffix[0];

        if (node == null || node.IsDeleted || !node.Children.TryGetValue(c, out var nextNode))
            return false;
        var label = nextNode.Label.AsSpan();
        var i = GetCommonPrefixLength(label, suffix);

        // suffix starts with label?
        if (i == label.Length)
        {
            // if the keys are equal, the key has already been inserted
            if (i == suffix.Length)
            {
                if (overwrite)
                    nextNode.SetValue(value);

                addedNode = nextNode;
                return true;
            }

            // structure has changed since last
            return false;
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

        outNode.SetValue(value);

        using (new LockWrapper(nodeLock, LockType.Write))
            node.Children[c] = splitNode;

        addedNode = outNode;
        return true;
    }

    /// <summary>
    /// Removes a node from the trie, if found
    /// </summary>
    /// <param name="subtreeRoot">The root of the subtree from which to remove the node</param>
    /// <param name="key">The key to remove</param>
    /// <param name="valueWrapper">(Optional) The value to remove. If specified, the node will only be removed if its value matches the wrapped value</param>
    /// <returns></returns>
    protected virtual bool Remove(BaseNode subtreeRoot, ReadOnlySpan<char> key, ValueWrapper valueWrapper = null)
    {
        BaseNode node = null, grandparent = null;
        var parent = subtreeRoot;
        using (new LockWrapper(_structureLock, LockType.Read))
        {
            for (var i = 0; i < key.Length;)
            {

                using (new LockWrapper(GetLock(parent), LockType.Read))
                {
                    if (!parent.Children.TryGetValue(key[i], out node))
                        return false;
                }

                var label = node.Label.AsSpan();
                var k = GetCommonPrefixLength(key[i..], label);

                // is this the node we're looking for?
                if (k == label.Length && k == key.Length - i)
                {
                    if (valueWrapper != null)
                    {
                        if (!node.TryGetValue(out var value) || !EqualityComparer<TValue>.Default.Equals(value, valueWrapper.Value))
                            return false;
                    }
                    // this node has to be removed or merged
                    if (node.TryRemoveValue(out _))
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

        // if we need to delete a node, the tree has to be restructured to remove empty leaves or merge
        // single children with branching node parents, and other threads may be currently on these nodes
        return RemoveAndRestructure(node, parent, grandparent);
    }

    protected bool RemoveAndRestructure(BaseNode node, BaseNode parent, BaseNode grandparent = null)
    {
        using var _structureLockWrapper = new LockWrapper(_structureLock, LockType.Write);
        var nodeLock = GetLock(node);
        var parentLock = GetLock(parent);
        var grandparentLock = grandparent != null ? GetLock(grandparent) : null;
        var lockAlreadyHeld = nodeLock == parentLock || nodeLock == grandparentLock;
        using var _nodeLockWrapper = new LockWrapper(nodeLock, lockAlreadyHeld ? LockType.UpgradeableRead : LockType.Read);

        // another thread has written a value to the node while we were waiting
        if (node.HasValue)
            return false;

        var c = node.Label[0];
        var nChildren = node.Children.Count;

        // if the node has no children, we can just remove it
        if (nChildren == 0)
        {
            using var _parentLockWrapper = new LockWrapper(parentLock, LockType.Write);
            // was removed or replaced by another thread
            if (!parent.Children.TryGetValue(c, out var n) || n != node)
                return false;

            parent.Children.Remove(c);
            node.Delete();

            // since we removed a node, we may be able to merge a lone sibling with the parent
            if (parent.Children.Count == 1 && grandparent != null && !parent.HasValue)
            {
                using var _grandparentLockWrapper = new LockWrapper(grandparentLock, grandparentLock == parentLock ? LockType.None : LockType.Write);
                if (!grandparent.Children.TryGetValue(c, out n) || n != parent || parent.HasValue)
                    return false;

                var child = parent.Children.First().Value;
                grandparent.Children[c] = new Node(parent.Label + child.Label, child);
                parent.Delete();
            }
        }
        // if there is a single child, we can merge it with node
        else if (nChildren == 1)
        {
            using var _parentLockWrapper = new LockWrapper(parentLock, LockType.Write);
            // was removed or replaced by another thread
            if (!parent.Children.TryGetValue(c, out var n) || n != node)
                return false;

            var child = node.Children.FirstOrDefault().Value;
            parent.Children[c] = new Node(node.Label + child.Label, child);
            node.Delete();
        }
        return true;
    }

    protected bool SearchOrPrune(ReadOnlySpan<char> prefix, bool prune, out BaseNode subtreeRoot)
    {
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

        for (var i = 0; i < prefix.Length; parent = node)
        {
            var c = prefix[i];
            var parentLock = GetLock(parent);
            using var _ = new LockWrapper(parentLock, LockType.UpgradeableRead);

            if (!parent.Children.TryGetValue(c, out node))
                return false;

            var label = node.Label.AsSpan();
            var k = GetCommonPrefixLength(prefix[i..], label);

            if (k == prefix.Length - i)
            {
                subtreeRoot = new Node(string.Concat(prefix[..i], label), node);
                if (!prune)
                    return true;

                using (new LockWrapper(parentLock, LockType.Write))
                    return parent.Children.Remove(c, out node);
            }

            if (k < label.Length)
                return false;

            i += label.Length;
        }

        return false;
    }

    #endregion
}
