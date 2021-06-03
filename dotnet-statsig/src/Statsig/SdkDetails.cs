using System;
using System.Reflection;

namespace Statsig
{
    internal static class SdkDetails
    {
        static bool _detailsFetched = false;
        static string _sdkType = null;
        static string _sdkVersion = null;

        public static string SdkType
        {
            get
            {
                EnsureDetailsAreFetched();
                return _sdkType;
            }
        }

        public static string SdkVersion
        {
            get
            {
                EnsureDetailsAreFetched();
                return _sdkVersion;
            }
        }

        static void EnsureDetailsAreFetched()
        {
            if (_detailsFetched)
            {
                return;
            }

            var name = Assembly.GetExecutingAssembly().GetName();
            _sdkVersion = name.Version.ToString();
            _sdkType = name.Name;
            _detailsFetched = true;
        }
    }
}
