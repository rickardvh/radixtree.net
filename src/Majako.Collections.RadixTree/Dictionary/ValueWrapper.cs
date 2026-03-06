namespace Majako.Collections.RadixTree;

public abstract partial class PrefixTree<TValue>
{
    protected sealed class ValueWrapper(TValue value)
    {
        public TValue Value { get; } = value;
    }
}
