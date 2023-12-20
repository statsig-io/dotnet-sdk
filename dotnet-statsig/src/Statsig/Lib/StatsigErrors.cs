using System;

namespace Statsig.Lib;

public class StatsigUninitializedException : InvalidOperationException
{
    public StatsigUninitializedException() : base("Must call Initialize() first.")
    {
    }
}

public class StatsigArgumentException : ArgumentException
{
    public StatsigArgumentException(string message) : base(message)
    {
    }
}

public class StatsigInvalidOperationException : InvalidOperationException
{
    public StatsigInvalidOperationException(string message) : base(message)
    {
    }
}

public class StatsigArgumentNullException : ArgumentNullException
{
    public StatsigArgumentNullException(string paramName, string message) : base(paramName, message)
    {
    }
}

public class StatsigInvalidSDKKeyResponse : SystemException
{
    public StatsigInvalidSDKKeyResponse() : base("Incorrect SDK Key used to generate response.")
    {
    }
}