using System;
namespace Statsig
{
    public enum InitializeResult
    {
        Success,
        Failure,
        AlreadyInitialized,
        InvalidSDKKey,
        NetworkError,
        Timeout,
        LocalMode,
    }
}
