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
                    c.OS.Major, c.OS.Minor, c.OS.Patch
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
            if (val1 == null || val2 == null)
            {
                return false;
            }
            if (double.TryParse(val1.ToString(), out double double1) && double.TryParse(val2.ToString(), out double double2))
            {
                return func(double1, double2);
            }
            return false;
        }

        internal static bool CompareTimes(object val1, object val2, Func<DateTimeOffset, DateTimeOffset, bool> func)
        {
            if (val1 == null || val2 == null)
            {
                return false;
            }
            try
            {
                var t1 = ParseDateTimeOffset(val1);
                var t2 = ParseDateTimeOffset(val2);
                return func(t1, t2);
            }
            catch
            {
                return false;
            }
        }

        internal static bool CompareVersions(object val1, object val2, Func<Version, Version, bool> func)
        {
            if (val1 == null || val2 == null)
            {
                return false;
            }
            NormalizeVersionString(val1.ToString(), out string version1);
            NormalizeVersionString(val2.ToString(), out string version2);
            if (Version.TryParse(version1, out Version v1) &&
                Version.TryParse(version2, out Version v2))
            {
                return func(v1, v2);
            }
            return false;
        }

        // Return true if the array contains the value, using case-insensitive comparison for strings
        internal static bool ArrayContains(object[] array, object value, bool ignoreCase)
        {
            if (array == null || value == null)
            {
                return false;
            }

            if (value is string)
            {
                return MatchStringInArray(array, value, ignoreCase, (string s1, string s2) => (s1.Equals(s2)));
            }
            else
            {
                return array.Contains(value);
            }
        }

        internal static bool MatchStringInArray(object[] array, object value, bool ignoreCase, Func<string, string, bool> func)
        {
            if (!(value is string))
            {
                return false;
            }

            foreach (var t in array)
            {
                if (!(t is string))
                {
                    continue;
                }
                if (ignoreCase && func(((string)value).ToLowerInvariant(), ((string)t).ToLowerInvariant()))
                {
                    return true;
                }
                if (func((string)value, (string)t))
                {
                    return true;
                }
            }

            return false;
        }

        private static void NormalizeVersionString(string version, out string normalized)
        {
            int hyphenIndex = version.IndexOf('-');
            normalized = version;
            
            if (hyphenIndex >= 0)
            {
                normalized = version.Substring(0, hyphenIndex);
            }
            if (int.TryParse(normalized, out _))
            {
                normalized += ".0"; // normalize versions represented by a single number, e.g. 2 => 2.0
            }
        }

        private static DateTimeOffset ParseDateTimeOffset(object val)
        {
            if (long.TryParse(val.ToString(), out long epochTime))
            {
                try
                {
                    // Throws if epochTime is out of range, usually means unit is in milliseconds instead
                    return DateTimeOffset.FromUnixTimeSeconds(epochTime);
                }
                catch
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(epochTime);
                }
            }
            return DateTimeOffset.Parse(val.ToString());
        }
    }
}
