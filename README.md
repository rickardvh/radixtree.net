# radixtree.net
C# implementations of radix trees, or space-optimised tries (see [the Wikipedia article](https://en.wikipedia.org/wiki/Radix_tree)).

Radixtree.NET provides multiple radix-tree implementations for different use cases:

## `RadixTree`
Implements `ISet<string>`

## `RadixTree<T>`
Implements `IDictionary<string, T>`

## `ConcurrentRadixTree<T>`
Implements `IDictionary<string, T>` and is thread-safe

## Notes
The predecessor to this library was initially developed at [Majako](https://majako.se) for use in [nopCommerce](https://www.nopcommerce.com/en) to efficiently handle cache keys.
