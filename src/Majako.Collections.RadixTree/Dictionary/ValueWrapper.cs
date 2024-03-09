namespace Majako.Collections.RadixTree;

public abstract partial class PrefixTree<TValue>
{
    protected class ValueWrapper(TValue value)
    {
        public readonly TValue Value = value;
    }
}
