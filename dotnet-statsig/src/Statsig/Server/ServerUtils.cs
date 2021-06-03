using System;
using System.Collections.Generic;

namespace Statsig.Server
{
    static class ServerUtils
    {
        internal static IReadOnlyDictionary<string, string> GetStatsigMetadata()
        {
            return new Dictionary<string, string>
            {
                ["sdkType"] = SdkDetails.SdkType,
                ["sdkVersion"] = SdkDetails.SdkVersion,
            };
        }
    }
}
