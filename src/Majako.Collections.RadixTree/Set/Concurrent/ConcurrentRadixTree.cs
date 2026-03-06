using System.Diagnostics.CodeAnalysis;

namespace Majako.Collections.RadixTree.Concurrent;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
[SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Backing dictionary field is intentionally exposed to derived set wrapper internals.")]
public partial class ConcurrentRadixTree : PrefixTree
{
    #region Fields

    protected readonly ConcurrentRadixTree<byte> _backingDict;

    #endregion

    #region Properties

    protected override IPrefixTree<byte> BackingDict => _backingDict;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new empty instance of <see cref="ConcurrentRadixTree{TValue}" />
    /// </summary>
    public ConcurrentRadixTree() : this(new ConcurrentRadixTree<byte>())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrentRadixTree" /> with the given items
    /// </summary>
    /// <param name="items">The items to be added to the trie</param>
    public ConcurrentRadixTree(IEnumerable<string> items) : this() => UnionWith(items);

    /// <summary>
    /// Initializes a new instance of <see cref="ConcurrentRadixTree" /> with the given backing dictionary
    /// </summary>
    /// <param name="backingDict">The backing dictionary</param>
    protected ConcurrentRadixTree(ConcurrentRadixTree<byte> backingDict)
    {
        _backingDict = backingDict;
    }

    #endregion

    #region Methods

    #region Public

    /// <inheritdoc/>
    public override IPrefixTree Prune(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        var pruned = BackingDict.Prune(prefix);
        return new ConcurrentRadixTree(pruned.Keys);
    }

    #endregion

    #endregion
}
