using System;
using System.Collections.Generic;
using System.Linq;

using IP3Country;
using UAParser;

namespace Statsig.Server.Evaluation
{
    static class Helpers
    {
        internal static object GetFromUser(StatsigUser user, string field)
        {
            if (user == null)
            {
                return null;
            }
            return user.properties.TryGetValue(field, out string strVal) ? strVal :
                user.customProperties.TryGetValue(field, out object objVal) ? objVal : null;
        }

        internal static string GetFromIP(string ipAddress, string field, out bool fetchFromServer)
        {
            fetchFromServer = false;
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(field))
            {
                return null;
            }

            if (field.ToLowerInvariant() != "country")
            {
                // Currently we only support ip lookup for country
                fetchFromServer = true;
                return null;
            }

            return CountryLookup.LookupIPStr(ipAddress);
        }

        internal static string GetFromUserAgent(string userAgent, string field)
        {
            if (string.IsNullOrEmpty(userAgent) || string.IsNullOrEmpty(field))
            {
                return null;
            }

            var uaParser = Parser.GetDefault();
            ClientInfo c = uaParser.Parse(userAgent);
            Dictionary<string, string> uaValues = new Dictionary<string, string>
            {
                ["os_name"] = c.OS.Family,
                ["os_version"] = string.Join(".", new string[] {
                    c.OS.Major, c.OS.Minor, c.OS.Patch, c.OS.PatchMinor
                }.Where(v => !string.IsNullOrEmpty(v)).ToArray()),
                ["browser_name"] = c.UA.Family,
                ["browser_version"] = string.Join(".", new string[] {
                    c.UA.Major, c.UA.Minor, c.UA.Patch
                }.Where(v => !string.IsNullOrEmpty(v)).ToArray())
            };

            if (uaValues.TryGetValue(field, out string value))
            {
                return value;
            }
            
            return null;
        }

        internal static string GetFromEnvironment(StatsigUser user, string field)
        {
            if (user == null || user.statsigEnvironment == null)
            {
                return null;
            }
            return user.statsigEnvironment.TryGetValue(field.ToLowerInvariant(), out string strVal) ? strVal : null;
        }

        internal static bool CompareNumbers(object val1, object val2, Func<double, double, bool> func)
        {
            if (double.TryParse(val1.ToString(), out double double1) && double.TryParse(val2.ToString(), out double double2))
            {
                return func(double1, double2);
            }
            return false;
        }

        internal static bool CompareVersions(object val1, object val2, Func<Version, Version, bool> func)
        {
            var version1 = RemoveVersionExtension(val1.ToString());
            var version2 = RemoveVersionExtension(val2.ToString());
            if (Version.TryParse(version1, out Version v1) &&
                Version.TryParse(version2, out Version v2))
            {
                return func(v1, v2);
            }
            return false;
        }

        private static string RemoveVersionExtension(string version)
        {
            int hyphenIndex = version.IndexOf('-');
            if (hyphenIndex >= 0)
            {
                return version.Substring(0, hyphenIndex);
            }
            return version;
        }

        // Return true if the array contains the value, using case-insensitive comparison for strings
        internal static bool ArrayContains(object[] array, object value)
        {
            if (array == null || value == null)
            {
                return false;
            }

            if (value is string)
            {
                return MatchStringCaseInsensitiveInArray(array, (string)value, (string s1, string s2) => (s1.Equals(s2)));
            }
            else
            {
                return array.Contains(value);
            }
        }

        internal static bool MatchStringCaseInsensitiveInArray(object[] array, string value, Func<string, string, bool> func)
        {
            foreach (var t in array)
            {
                if (func(value.ToLowerInvariant(), t.ToString().ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
