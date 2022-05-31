using System;

namespace Statsig.Lib
{
    public interface IIDStore: IDisposable
    {
        int Count { get; }
        bool Add(string id);
        bool Remove(string id);
        bool Contains(string id);
        void TrimExcess();
    }
}