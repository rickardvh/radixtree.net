using System.Diagnostics.CodeAnalysis;

namespace Majako.Collections.RadixTree;

/// <summary>
/// Represents a thread-safe collection
/// </summary>
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Public API keeps established PrefixTree naming.")]
public partial interface IPrefixTree : ISet<string>
{
    /// <summary>
    /// Gets all items starting with the given prefix
    /// </summary>
    /// <param name="prefix">The prefix (case-sensitive) to search for</param>
    /// <returns>
    /// All items starting with <paramref name="prefix"/>
    /// </returns>
    IEnumerable<string> Search(string prefix);

    /// <summary>
    /// Removes all items with items starting with the specified prefix
    /// </summary>
    /// <param name="prefix">The prefix (case-sensitive) of the items to be deleted</param>
    /// <returns>
    /// The sub-collection containing all deleted items (possibly empty)
    /// </returns>
    IPrefixTree Prune(string prefix);
}
