namespace Majako.Collections.RadixTree;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public partial class RadixTree : PrefixTree
{
    #region Fields

    protected readonly RadixTree<byte> _backingDict;

    #region Properties

    protected override IPrefixTree<byte> BackingDict => _backingDict;

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new empty instance of <see cref="RadixTree" />
    /// </summary>
    public RadixTree() : this(new RadixTree<byte>())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RadixTree" /> with the given items
    /// </summary>
    /// <param name="items">The items to be added to the trie</param>
    public RadixTree(IEnumerable<string> items) : this()
    {
        foreach (var item in items)
            Add(item);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RadixTree" /> with the given backing dictionary
    /// </summary>
    /// <param name="backingDict">The backing dictionary</param>
    protected RadixTree(RadixTree<byte> backingDict)
    {
        _backingDict = backingDict;
    }

    #endregion

    #region Methods

    #region Public

    /// <inheritdoc/>
    public override IPrefixTree Prune(string prefix) => new RadixTree(_backingDict.Prune(prefix) as RadixTree<byte>);

    #endregion

    #endregion
}
