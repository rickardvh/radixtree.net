namespace Majako.Collections.RadixTree.Concurrent;

internal enum LockType
{
    None,
    Read,
    Write,
    UpgradeableRead
}

internal readonly struct LockWrapper : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;
    private readonly LockType _acquiredLockType;

    public LockWrapper(ReaderWriterLockSlim @lock, LockType lockType)
    {
        _lock = @lock;
        _acquiredLockType = lockType;
        switch (lockType)
        {
            case LockType.Read:
                @lock.EnterReadLock();
                break;
            case LockType.Write:
                @lock.EnterWriteLock();
                break;
            case LockType.UpgradeableRead:
                @lock.EnterUpgradeableReadLock();
                break;
            default:
                break;
        }
    }

    public void Dispose()
    {
        switch (_acquiredLockType)
        {
            case LockType.Read:
                _lock.ExitReadLock();
                break;
            case LockType.Write:
                _lock.ExitWriteLock();
                break;
            case LockType.UpgradeableRead:
                _lock.ExitUpgradeableReadLock();
                break;
            default:
                break;
        }
    }
}
