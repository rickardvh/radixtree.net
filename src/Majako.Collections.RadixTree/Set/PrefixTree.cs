using System.Collections;

namespace Majako.Collections.RadixTree;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public abstract partial class PrefixTree : IPrefixTree
{
    public virtual int Count => Search(string.Empty).Count();
    public virtual bool IsReadOnly => false;

    protected abstract IPrefixTree<byte> BackingDict { get; }

    public abstract IPrefixTree Prune(string prefix);

    /// <inheritdoc/>
    public virtual bool Add(string key) => BackingDict.TryAdd(key, default);

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
        var set = new HashSet<string>(other);
        var toRemove = this.Where(x => !set.Contains(x)).ToList();
        ExceptWith(toRemove);
    }

    public virtual bool IsProperSubsetOf(IEnumerable<string> other)
    {
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
        var set = new HashSet<string>(other);
        return this.All(set.Contains);
    }

    public virtual bool IsSupersetOf(IEnumerable<string> other)
    {
        var set = new HashSet<string>(other);
        return set.All(Contains);
    }

    public virtual void ExceptWith(IEnumerable<string> other)
    {
        foreach (var item in other)
            Remove(item);
    }

    public virtual bool Overlaps(IEnumerable<string> other)
    {
        return other.Any(Contains);
    }

    public virtual bool SetEquals(IEnumerable<string> other)
    {
        var set = new HashSet<string>(other);
        return Count == set.Count && this.All(set.Contains);
    }

    public virtual void SymmetricExceptWith(IEnumerable<string> other)
    {
        var set = new HashSet<string>(other);
        var toAdd = new List<string>();
        var toRemove = new List<string>();

        foreach (var item in this)
            (set.Contains(item) ? toRemove : toAdd).Add(item);

        ExceptWith(toRemove);
        UnionWith(toAdd);
    }

    public virtual void UnionWith(IEnumerable<string> other)
    {
        foreach (var item in other)
            Add(item);
    }

    public void CopyTo(string[] array, int arrayIndex)
    {
        foreach (var x in this)
            array[arrayIndex++] = x;
    }

    void ICollection<string>.Add(string item) => Add(item);

    public IEnumerator<string> GetEnumerator() => Search(string.Empty).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
