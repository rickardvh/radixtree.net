using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Majako.Collections.RadixTree;

/// <summary>
/// A thread-safe implementation of a radix tree
/// </summary>
public abstract partial class PrefixTree<TValue> : IPrefixTree<TValue>
{
    public virtual TValue this[string key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    public IEnumerable<string> Keys => this.Select(t => t.Key);
    public IEnumerable<TValue> Values => this.Select(t => t.Value);
    public virtual int Count => Keys.Count();
    public virtual bool IsReadOnly => false;

    ICollection<string> IDictionary<string, TValue>.Keys => Keys.ToList();

    ICollection<TValue> IDictionary<string, TValue>.Values => Values.ToList();

    public abstract void Add(string key, TValue value);
    public abstract IPrefixTree<TValue> Prune(string prefix);
    public abstract bool Remove(string key);
    public abstract bool Remove(KeyValuePair<string, TValue> item);
    public abstract IEnumerable<KeyValuePair<string, TValue>> Search(string prefix);
    public abstract bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value);
    public abstract void Clear();

    public virtual bool ContainsKey(string key) => TryGetValue(key, out _);

    public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
    {
        foreach (var kv in this)
            array[arrayIndex++] = kv;
    }

    public void Add(KeyValuePair<string, TValue> item) => Add(item.Key, item.Value);

    public bool Contains(KeyValuePair<string, TValue> item) => TryGetValue(item.Key, out var value)
            && EqualityComparer<TValue>.Default.Equals(value, item.Value);

    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => Search(string.Empty).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => "{" + string.Join(", ", this.Select(kv => $"\"{kv.Key}\": {kv.Value}")) + "}";
}
