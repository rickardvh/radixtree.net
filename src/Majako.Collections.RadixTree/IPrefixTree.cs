﻿namespace Majako.Collections.RadixTree;

/// <summary>
/// Represents a thread-safe collection
/// </summary>
public partial interface IPrefixTree<TValue>
{
    #region Properties

    /// <summary>
    /// Gets a collection that contains the keys in the <see cref="IConcurrentCollection{TValue}" />
    /// </summary>
    IEnumerable<string> Keys { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Attempts to get the value associated with the specified key
    /// </summary>
    /// <param name="key">The key of the item to get (case-sensitive)</param>
    /// <param name="value">The value associated with <paramref name="key"/>, if found</param>
    /// <returns>
    /// True if the key was found, otherwise false
    /// </returns>
    bool TryGetValue(string key, out TValue value);

    /// <summary>
    /// Adds a key-value pair to the collection
    /// </summary>
    /// <param name="key">The key of the new item (case-sensitive)</param>
    /// <param name="value">The value to be associated with <paramref name="key"/></param>
    void Add(string key, TValue value);

    /// <summary>
    /// Clears the collection
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets all key-value pairs for keys starting with the given prefix
    /// </summary>
    /// <param name="prefix">The prefix (case-sensitive) to search for</param>
    /// <returns>
    /// All key-value pairs for keys starting with <paramref name="prefix"/>
    /// </returns>
    IEnumerable<KeyValuePair<string, TValue>> Search(string prefix);

    /// <summary>
    /// Removes the item with the given key, if present
    /// </summary>
    /// <param name="key">The key (case-sensitive) of the item to be removed</param>
    void Remove(string key);

    /// <summary>
    /// Removes all items with keys starting with the specified prefix
    /// </summary>
    /// <param name="prefix">The prefix (case-sensitive) of the items to be deleted</param>
    /// <returns>
    /// The sub-collection containing all deleted items (possibly empty)
    /// </returns>
     IPrefixTree<TValue> Prune(string prefix);

    #endregion
}
