
using System;
using System.Collections.Generic;
using System.Threading;

namespace Statsig.Lib
{
    class InMemoryIDStore: IDisposable, IIDStore
    {
        readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(
          LockRecursionPolicy.SupportsRecursion
        );
        internal readonly HashSet<string> _hashSet = new HashSet<string>();

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try 
                {
                    return _hashSet.Count;
                }
                finally
                {
                    if (_lock.IsReadLockHeld)
                    {
                        _lock.ExitReadLock();
                    }
                }
            }
        }

        public bool Add(string item)
        {
            _lock.EnterWriteLock();
            try 
            {
                return _hashSet.Add(item);
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public bool Remove(string item)
        {
            _lock.EnterWriteLock();
            try 
            {
                return _hashSet.Remove(item);
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try 
            {
                _hashSet.Clear();
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public void TrimExcess()
        {
            _lock.EnterWriteLock();
            try 
            {
                _hashSet.TrimExcess();
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public bool Contains(string item)
        {
            _lock.EnterReadLock();
            try 
            {
                return _hashSet.Contains(item);
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lock.Dispose();
                _hashSet.Clear();
            }
        }
    }
}