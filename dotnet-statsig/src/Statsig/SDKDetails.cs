using System;
using System.Collections.Generic;

namespace Statsig
{
    public class SDKDetails
    {
        private static SDKDetails? _clientDetails;
        private static SDKDetails? _serverDetails;

        internal string SDKType;
        internal string SDKVersion;

        internal SDKDetails(string type)
        {
            SDKType = type;
            SDKVersion = GetType().Assembly.GetName()!.Version!.ToString();
        }

        internal IReadOnlyDictionary<string, string> StatsigMetadata
        {
            get
            {
                return new Dictionary<string, string>
                {
                    ["sdkType"] = SDKType,
                    ["sdkVersion"] = SDKVersion
                };
            }
        }

        internal static SDKDetails GetClientSDKDetails()
        {
            if (_clientDetails == null)
            {
                _clientDetails = new SDKDetails("dotnet-client");
            }
            return _clientDetails;
        }

        internal static SDKDetails GetServerSDKDetails()
        {
            if (_serverDetails == null)
            {
                _serverDetails = new SDKDetails("dotnet-server");
            }
            return _serverDetails;
        }
    }
}
