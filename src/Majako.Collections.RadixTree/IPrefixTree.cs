namespace Majako.Collections.RadixTree;

/// <summary>
/// Represents a thread-safe collection
/// </summary>
public partial interface IPrefixTree<TValue> : IDictionary<string, TValue>
{
    /// <summary>
    /// Gets all key-value pairs for keys starting with the given prefix
    /// </summary>
    /// <param name="prefix">The prefix (case-sensitive) to search for</param>
    /// <returns>
    /// All key-value pairs for keys starting with <paramref name="prefix"/>
    /// </returns>
    IEnumerable<KeyValuePair<string, TValue>> Search(string prefix);

    /// <summary>
    /// Removes all items with keys starting with the specified prefix
    /// </summary>
    /// <param name="prefix">The prefix (case-sensitive) of the items to be deleted</param>
    /// <returns>
    /// The sub-collection containing all deleted items (possibly empty)
    /// </returns>
     IPrefixTree<TValue> Prune(string prefix);
}
