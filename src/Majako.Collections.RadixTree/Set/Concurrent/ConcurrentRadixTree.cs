namespace Majako.Collections.RadixTree.Concurrent;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
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
    public override IPrefixTree Prune(string prefix) => new ConcurrentRadixTree(_backingDict.Prune(prefix) as ConcurrentRadixTree<byte>);

    #endregion

    #endregion
}
