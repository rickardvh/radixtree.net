using System.Collections;

namespace Majako.Collections.RadixTree;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public abstract partial class PrefixTree : IPrefixTree
{
    public virtual int Count => BackingDict.Count;
    public virtual bool IsReadOnly => false;

    protected abstract IPrefixTree<byte> BackingDict { get; }

    public abstract IPrefixTree Prune(string prefix);

    /// <inheritdoc/>
    public virtual bool Add(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

        try
        {
            BackingDict.Add(key, default);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public virtual bool Contains(string item) => BackingDict.ContainsKey(item);

    /// <inheritdoc/>
    public virtual void Clear() => BackingDict.Clear();

    /// <inheritdoc/>
    public virtual bool Remove(string key) => BackingDict.Remove(key);

    /// <inheritdoc/>
    public virtual IEnumerable<string> Search(string prefix) => BackingDict.Search(prefix).Select(kv => kv.Key);

    public virtual void IntersectWith(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        var toRemove = this.Where(x => !set.Contains(x)).ToList();
        ExceptWith(toRemove);
    }

    public virtual bool IsProperSubsetOf(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        var count = 0;

        foreach (var item in this)
        {
            if (!set.Contains(item))
                return false;
            count++;
        }

        return count < set.Count;
    }

    public virtual bool IsProperSupersetOf(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        var count = 0;

        foreach (var item in set)
        {
            if (!Contains(item))
                return false;
            count++;
        }

        return count < Count;
    }

    public virtual bool IsSubsetOf(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        return this.All(set.Contains);
    }

    public virtual bool IsSupersetOf(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        return set.All(Contains);
    }

    public virtual void ExceptWith(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other)
            Remove(item);
    }

    public virtual bool Overlaps(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return other.Any(Contains);
    }

    public virtual bool SetEquals(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        return Count == set.Count && this.All(set.Contains);
    }

    public virtual void SymmetricExceptWith(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var set = new HashSet<string>(other);
        var toRemove = this.Where(set.Contains).ToList();
        var toAdd = set.Where(item => !Contains(item)).ToList();

        ExceptWith(toRemove);
        UnionWith(toAdd);
    }

    public virtual void UnionWith(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other)
            Add(item);
    }

    public void CopyTo(string[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0 || arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("Destination array is not long enough.", nameof(array));

        foreach (var x in this)
            array[arrayIndex++] = x;
    }

    void ICollection<string>.Add(string item) => Add(item);

    public IEnumerator<string> GetEnumerator() => Search(string.Empty).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
