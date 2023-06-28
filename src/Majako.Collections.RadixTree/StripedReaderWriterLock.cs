namespace Majako.Collections.RadixTree
{
    /// <summary>
    /// A striped ReaderWriterLock wrapper
    /// </summary>
    public class StripedReaderWriterLock
    {
        #region Fields

        protected readonly ReaderWriterLockSlim[] _locks;

        #endregion

        #region Ctor

        // defaults to 8 times the number of processor cores
        public StripedReaderWriterLock(int nLocks = 0)
        {
            if (nLocks == 0)
                nLocks = Environment.ProcessorCount * 8;

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
}
