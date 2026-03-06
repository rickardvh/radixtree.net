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
    protected long _splitCount;
    protected long _removedLeafCount;
    protected int _activeMutations;
    protected int _compactionInProgress;

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
            this[key] = value;
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

    protected readonly struct MutationScope(ConcurrentRadixTree<TValue> tree) : IDisposable
    {
        private readonly ConcurrentRadixTree<TValue> _tree = tree;

        public void Dispose() => Interlocked.Decrement(ref _tree._activeMutations);
    }

    protected virtual MutationScope EnterMutationScope()
    {
        var spinWait = new SpinWait();

        while (true)
        {
            while (Volatile.Read(ref _compactionInProgress) != 0)
                spinWait.SpinOnce();

            Interlocked.Increment(ref _activeMutations);

            if (Volatile.Read(ref _compactionInProgress) == 0)
                return new MutationScope(this);

            Interlocked.Decrement(ref _activeMutations);
            spinWait.SpinOnce();
        }
    }

    #region Public

    public override int Count => CountNodes(_root);

    /// <inheritdoc/>
    public override bool TryGetValue(string key, out TValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        value = default;
        var spinWait = new SpinWait();
        var retries = 0;

        while (true)
        {
            if (!Find(key, _root, out var node))
                return false;

            if (node.TryGetValue(out value))
                return true;

            if (!node.IsDeleted)
                return false;

            if (retries >= 512)
                return false;

            BackoffMutation(ref spinWait, ref retries);
        }
    }

    /// <inheritdoc/>
    public override void Add(string key, TValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        using (EnterMutationScope())
            GetOrAddNode(key, value, overwrite: false, throwOnExisting: true);

        TryCompactIfNeeded();
    }

    protected override void SetItem(string key, TValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        using (EnterMutationScope())
            GetOrAddNode(key, value, overwrite: true);

        TryCompactIfNeeded();
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        using (EnterMutationScope())
            _root = new Node();

        Interlocked.Exchange(ref _splitCount, 0);
        Interlocked.Exchange(ref _removedLeafCount, 0);
    }

    /// <inheritdoc/>
    public override bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        bool removed;
        using (EnterMutationScope())
            removed = Remove(_root, key);

        if (removed)
            TryCompactIfNeeded();

        return removed;
    }

    /// <inheritdoc/>
    public override bool Remove(KeyValuePair<string, TValue> item)
    {
        ArgumentException.ThrowIfNullOrEmpty(item.Key, nameof(item.Key));

        bool removed;
        using (EnterMutationScope())
            removed = Remove(_root, item.Key, new ValueWrapper(item.Value));

        if (removed)
            TryCompactIfNeeded();

        return removed;
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

        BaseNode subtreeRoot;

        using (EnterMutationScope())
        {
            if (!SearchOrPrune(prefix, true, out subtreeRoot))
                return new ConcurrentRadixTree<TValue>();
        }

        Interlocked.Exchange(ref _splitCount, 0);
        Interlocked.Exchange(ref _removedLeafCount, 0);
        return new ConcurrentRadixTree<TValue>(subtreeRoot);
    }

    #endregion

    protected virtual int CountNodes(BaseNode node)
    {
        var count = node.HasValue ? 1 : 0;
        IList<BaseNode> children;

        using (new LockWrapper(GetLock(node), LockType.Read))
            children = [.. node.Children.Values];

        foreach (var child in children)
            count += CountNodes(child);

        return count;
    }

    /// <summary>
    /// Gets a lock on the node's children
    /// </summary>
    /// <remarks>
    /// May return the same lock for two different nodes, so the user needs to check to avoid lock recursion exceptions
    /// </remarks>
    protected virtual ReaderWriterLockSlim GetLock(BaseNode node) => _locks.GetLock(node.Children);

    protected virtual bool IsCurrentChild(BaseNode parent, char edge, BaseNode expectedChild)
        => parent.Children.TryGetValue(edge, out var current) && current == expectedChild;

    protected virtual bool IsStaleOrReplacedEdge(BaseNode parent, char edge, BaseNode expectedChild)
    {
        var parentLock = GetLock(parent);
        using (new LockWrapper(parentLock, LockType.Read))
        {
            if (parent.IsDeleted)
                return true;

            return !parent.Children.TryGetValue(edge, out var current) || current != expectedChild;
        }
    }

    protected virtual void InsertCompactedNode(Node root, ReadOnlySpan<char> key, TValue value)
    {
        BaseNode node = root;
        var suffix = key;

        while (true)
        {
            var c = suffix[0];

            if (node.Children.TryGetValue(c, out BaseNode nextNode))
            {
                var label = nextNode.Label.AsSpan();
                var i = GetCommonPrefixLength(label, suffix);
                if (i == label.Length)
                {
                    if (i == suffix.Length)
                    {
                        nextNode.SetValue(value);
                        return;
                    }

                    suffix = suffix[label.Length..];
                    node = nextNode;
                    continue;
                }

                var splitNode = new Node(suffix[..i])
                {
                    Children = { [label[i]] = new Node(label[i..], nextNode) }
                };

                node.Children[c] = splitNode;

                if (i == suffix.Length)
                    node = splitNode;
                else
                    splitNode.Children[suffix[i]] = node = new Node(suffix[i..]);

                node.SetValue(value);
                return;
            }

            var suffixNode = new Node(suffix);
            suffixNode.SetValue(value);
            node.Children[c] = suffixNode;
            return;
        }
    }

    protected virtual BaseNode BuildCompactedRoot()
    {
        var rebuiltRoot = new Node();
        var snapshot = Search(string.Empty).ToArray();

        foreach (var (key, value) in snapshot)
            InsertCompactedNode(rebuiltRoot, key, value);

        return rebuiltRoot;
    }

    protected virtual void TryCompactIfNeeded()
    {
        var debt = Interlocked.Read(ref _removedLeafCount) - Interlocked.Read(ref _splitCount);
        if (debt < 4096)
            return;

        var count = Count;
        if (count == 0 || debt < count / 2)
            return;

        if (Interlocked.CompareExchange(ref _compactionInProgress, 1, 0) != 0)
            return;

        try
        {
            var spinWait = new SpinWait();
            var spins = 0;
            while (Volatile.Read(ref _activeMutations) != 0)
            {
                if (++spins >= 1024)
                    return;

                spinWait.SpinOnce();
            }

            if (!_structureLock.TryEnterWriteLock(0))
                return;

            try
            {
                debt = Interlocked.Read(ref _removedLeafCount) - Interlocked.Read(ref _splitCount);
                count = Count;

                if (debt < 4096 || count == 0 || debt < count / 2)
                    return;

                _root = BuildCompactedRoot();
                Interlocked.Exchange(ref _splitCount, 0);
                Interlocked.Exchange(ref _removedLeafCount, 0);
            }
            finally
            {
                _structureLock.ExitWriteLock();
            }
        }
        finally
        {
            Volatile.Write(ref _compactionInProgress, 0);
        }
    }

    protected virtual void BackoffMutation(ref SpinWait spinWait, ref int retries)
    {
        retries++;
        if (retries < 64)
        {
            spinWait.SpinOnce();
            return;
        }

        retries = 0;
        spinWait = new SpinWait();
        Thread.Yield();
    }

    protected virtual bool TryWithOrderedWriteLocks(
        ReaderWriterLockSlim lock1,
        ReaderWriterLockSlim lock2,
        Func<bool> action)
    {
        if (lock1 == lock2)
        {
            if (!lock1.TryEnterWriteLock(0))
                return false;

            try
            {
                return action();
            }
            finally
            {
                lock1.ExitWriteLock();
            }
        }

        var hash1 = RuntimeHelpers.GetHashCode(lock1);
        var hash2 = RuntimeHelpers.GetHashCode(lock2);
        var first = hash1 <= hash2 ? lock1 : lock2;
        var second = ReferenceEquals(first, lock1) ? lock2 : lock1;
        var equalHashes = hash1 == hash2;
        var spinWait = new SpinWait();
        var attempts = 0;

        while (attempts < 4096)
        {
            if (first.TryEnterWriteLock(0))
            {
                try
                {
                    if (second.TryEnterWriteLock(0))
                    {
                        try
                        {
                            return action();
                        }
                        finally
                        {
                            second.ExitWriteLock();
                        }
                    }
                }
                finally
                {
                    first.ExitWriteLock();
                }
            }

            attempts++;
            if (equalHashes)
                (first, second) = (second, first);

            if (attempts % 64 == 0)
                Thread.Yield();
            else
                spinWait.SpinOnce();
        }

        return false;
    }

    protected virtual bool Find(ReadOnlySpan<char> key, BaseNode subtreeRoot, out BaseNode node)
    {
        if (key.Length == 0)
        {
            node = subtreeRoot;
            return true;
        }

        var spinWait = new SpinWait();
        var retries = 0;

        while (true)
        {
            node = subtreeRoot;
            var suffix = key;
            var shouldRetry = false;

            while (true)
            {
                if (node.IsDeleted)
                {
                    shouldRetry = true;
                    break;
                }

                var parent = node;
                var edge = suffix[0];
                using (new LockWrapper(GetLock(parent), LockType.Read))
                {
                    if (parent.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    if (!parent.Children.TryGetValue(edge, out node))
                        return false;
                }

                if (node.IsDeleted)
                {
                    shouldRetry = true;
                    break;
                }

                var span = node.Label.AsSpan();
                var i = GetCommonPrefixLength(suffix, span);

                if (i != span.Length)
                {
                    if (node.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    return false;
                }

                if (i == suffix.Length)
                {
                    if (node.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    return true;
                }

                suffix = suffix[i..];
            }

            if (!shouldRetry)
                return false;

            if (retries >= 1024)
                return false;

            BackoffMutation(ref spinWait, ref retries);
        }
    }

    protected virtual BaseNode GetOrAddNode(ReadOnlySpan<char> key, TValue value, bool overwrite = false, bool throwOnExisting = false)
    {
        var spinWait = new SpinWait();
        var retries = 0;

        while (true)
        {
            var node = _root;
            var suffix = key;
            while (true)
            {
                if (node.IsDeleted)
                    break;

                var c = suffix[0];
                var parentLock = GetLock(node);
                BaseNode splitParent = null;
                BaseNode splitChild = null;
                char splitEdge = default;
                ReadOnlySpan<char> splitSuffix = default;

                using (new LockWrapper(parentLock, LockType.UpgradeableRead))
                {
                    if (node.IsDeleted)
                        break;

                    if (!node.Children.TryGetValue(c, out BaseNode nextNode))
                    {
                        using (new LockWrapper(parentLock, LockType.Write))
                        {
                            if (node.IsDeleted)
                                break;

                            if (node.Children.TryGetValue(c, out nextNode))
                                continue;

                            var suffixNode = new Node(suffix);
                            suffixNode.SetValue(value);

                            return node.Children[c] = suffixNode;
                        }
                    }

                    if (nextNode.IsDeleted)
                        break;

                    var label = nextNode.Label.AsSpan();
                    var i = GetCommonPrefixLength(label, suffix);
                    // suffix starts with label
                    if (i == label.Length)
                    {
                        // keys are equal - this is the node we're looking for
                        if (i == suffix.Length)
                        {
                            using (new LockWrapper(parentLock, LockType.Write))
                            {
                                if (node.IsDeleted || nextNode.IsDeleted || !IsCurrentChild(node, c, nextNode))
                                    break;

                                if (throwOnExisting && nextNode.HasValue)
                                    throw new ArgumentException("An item with the same key has already been added.", nameof(key));

                                if (overwrite || throwOnExisting)
                                    nextNode.SetValue(value);
                                else
                                    nextNode.GetOrAddValue(value);
                            }

                            if (nextNode.IsDeleted)
                                break;

                            return nextNode;
                        }

                        // advance the suffix and continue the search from nextNode
                        suffix = suffix[label.Length..];
                        node = nextNode;
                        continue;
                    }

                    splitParent = node;
                    splitChild = nextNode;
                    splitEdge = c;
                    splitSuffix = suffix;
                }

                if (splitParent != null
                    && TrySplitAndInsert(splitParent, splitChild, splitEdge, splitSuffix, value, overwrite, throwOnExisting, out var addedNode))
                    return addedNode;

                break;
            }

            BackoffMutation(ref spinWait, ref retries);
        }
    }

    protected virtual bool TrySplitAndInsert(
        BaseNode parent,
        BaseNode child,
        char parentEdge,
        ReadOnlySpan<char> suffix,
        TValue value,
        bool overwrite,
        bool throwOnExisting,
        out BaseNode addedNode)
    {
        addedNode = default;

        if (parent == null || child == null || parent.IsDeleted || child.IsDeleted)
            return false;

        var suffixString = suffix.ToString();
        var suffixLength = suffix.Length;
        const string suffixParamName = "suffix";
        BaseNode resultNode = null;
        var parentLock = GetLock(parent);
        var childLock = GetLock(child);

        var success = TryWithOrderedWriteLocks(parentLock, childLock, () =>
        {
            if (parent.IsDeleted || child.IsDeleted || !IsCurrentChild(parent, parentEdge, child))
                return false;

            var label = child.Label.AsSpan();
            var suffixSpan = suffixString.AsSpan();
            var i = GetCommonPrefixLength(label, suffixSpan);

            // suffix starts with label?
            if (i == label.Length)
            {
                // if the keys are equal, the key has already been inserted
                if (i == suffixLength)
                {
                    if (throwOnExisting && child.HasValue)
                        throw new ArgumentException("An item with the same key has already been added.", suffixParamName);

                    if (overwrite || throwOnExisting)
                        child.SetValue(value);
                    else
                        child.GetOrAddValue(value);

                    if (child.IsDeleted)
                        return false;

                    resultNode = child;
                    return true;
                }

                // structure has changed since last
                return false;
            }

            var splitNode = new Node(suffixSpan[..i]);
            var branchFromChild = new Node(label[i..], child);
            splitNode.Children[label[i]] = branchFromChild;

            BaseNode outNode;

            // label starts with suffix, so we can return splitNode
            if (i == suffixLength)
                outNode = splitNode;
            // the keys diverge, so we need to branch from splitNode
            else
                splitNode.Children[suffixSpan[i]] = outNode = new Node(suffixSpan[i..]);

            outNode.SetValue(value);
            child.Delete();
            parent.Children[parentEdge] = splitNode;
            Interlocked.Increment(ref _splitCount);

            resultNode = outNode;
            return true;
        });

        addedNode = resultNode;
        return success;
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
        var spinWait = new SpinWait();
        var retries = 0;

        while (true)
        {
            BaseNode node = null;
            var parent = subtreeRoot;
            var shouldRetry = false;

            for (var i = 0; i < key.Length;)
            {
                if (parent.IsDeleted)
                {
                    shouldRetry = true;
                    break;
                }

                var parentLock = GetLock(parent);
                using (new LockWrapper(parentLock, LockType.Read))
                {
                    if (parent.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    if (!parent.Children.TryGetValue(key[i], out node))
                        return false;
                }

                if (node.IsDeleted)
                {
                    shouldRetry = true;
                    break;
                }

                var label = node.Label.AsSpan();
                var k = GetCommonPrefixLength(key[i..], label);

                // is this the node we're looking for?
                if (k == label.Length && k == key.Length - i)
                {
                    if (node.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    if (IsStaleOrReplacedEdge(parent, key[i], node))
                    {
                        shouldRetry = true;
                        break;
                    }

                    if (valueWrapper != null)
                    {
                        if (!node.TryGetValue(out var value) || !EqualityComparer<TValue>.Default.Equals(value, valueWrapper.Value))
                            return false;
                    }

                    if (node.TryRemoveValue(out _))
                    {
                        RemoveAndRestructure(node, parent);
                        return true;
                    }

                    if (node.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    if (node.HasValue)
                    {
                        shouldRetry = true;
                        break;
                    }

                    return false;
                }

                if (k < label.Length)
                {
                    if (IsStaleOrReplacedEdge(parent, key[i], node) || node.IsDeleted)
                    {
                        shouldRetry = true;
                        break;
                    }

                    return false;
                }

                i += label.Length;
                if (IsStaleOrReplacedEdge(parent, key[i - label.Length], node))
                {
                    shouldRetry = true;
                    break;
                }

                parent = node;
            }

            if (!shouldRetry)
                return false;

            if (retries >= 1024)
                return false;

            BackoffMutation(ref spinWait, ref retries);
        }
    }

    protected bool RemoveAndRestructure(BaseNode node, BaseNode parent)
    {
        if (node == null || parent == null || node.IsDeleted || parent.IsDeleted)
            return false;

        var nodeLock = GetLock(node);
        var parentLock = GetLock(parent);
        return TryWithOrderedWriteLocks(nodeLock, parentLock, () =>
        {
            if (node.IsDeleted || parent.IsDeleted)
                return false;

            // another thread has written a value to the node while we were waiting
            if (node.HasValue)
                return false;

            if (node.Children.Count != 0)
                return true;

            var c = node.Label[0];

            // was removed or replaced by another thread
            if (!IsCurrentChild(parent, c, node))
                return false;

            parent.Children.Remove(c);
            node.Delete();
            Interlocked.Increment(ref _removedLeafCount);

            return true;
        });
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
