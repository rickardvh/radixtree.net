namespace Majako.Collections.RadixTree;

public partial class ConcurrentTrie<TValue>
{
    protected class ValueWrapper(TValue value)
    {
        public readonly TValue Value = value;
    }
}
