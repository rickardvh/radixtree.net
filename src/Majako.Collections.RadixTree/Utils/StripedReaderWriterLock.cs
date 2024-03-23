namespace Majako.Collections.RadixTree;

/// <summary>
/// Represents a striped reader-writer lock that allows multiple readers or a single writer to access a resource concurrently.
/// </summary>
public class StripedReaderWriterLock
{
    #region Fields

    protected const int MULTIPLIER = 8;
    protected readonly ReaderWriterLockSlim[] _locks;

    #endregion

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="StripedReaderWriterLock"/> class.
    /// </summary>
    /// <param name="nLocks">The number of locks to create. When <= 0, defaults to 8 times the number of processors.</param>
    public StripedReaderWriterLock(int nLocks = 0)
    {
        if (nLocks <= 0)
            nLocks = Environment.ProcessorCount * MULTIPLIER;

        _locks = new ReaderWriterLockSlim[nLocks];

        for (var i = 0; i < nLocks; i++)
            _locks[i] = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a lock on the object
    /// </summary>
    public ReaderWriterLockSlim GetLock(object obj)
    {
        return _locks[obj.GetHashCode() % _locks.Length];
    }

    #endregion
}
