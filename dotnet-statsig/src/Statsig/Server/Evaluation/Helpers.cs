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
            string strVal;
            object objVal;
            return
                user.properties.TryGetValue(field, out strVal) ? strVal :
                user.properties.TryGetValue(field.ToLowerInvariant(), out strVal) ? strVal :
                user.customProperties.TryGetValue(field, out objVal) ? objVal :
                user.customProperties.TryGetValue(field.ToLowerInvariant(), out objVal) ? objVal :
                user.privateAttributes.TryGetValue(field, out objVal) ? objVal :
                user.privateAttributes.TryGetValue(field.ToLowerInvariant(), out objVal) ? objVal : null;
        }

        internal static string GetFromIP(StatsigUser user, string field)
        {
            var ip = GetFromUser(user, "ip");
            if (!(ip is string))
            {
                return null;
            }
            string ipStr = (string)ip;

            if (string.IsNullOrEmpty(ipStr) || string.IsNullOrEmpty(field) || field.ToLowerInvariant() != "country")
            {
                return null;
            }

            return CountryLookup.LookupIPStr(ipStr);
        }

        internal static string GetFromUserAgent(StatsigUser user, string field)
        {
            var ua = GetFromUser(user, "userAgent");
            if (!(ua is string))
            {
                return null;
            }
            var uaParser = Parser.GetDefault();
            ClientInfo c = uaParser.Parse((string)ua);
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

        internal static bool MatchStringInArray(object[] array, object value, bool ignoreCase, Func<string, string, bool> func)
        {
            if (value == null)
            {
                return false;
            }
            try
            {
                foreach (var t in array)
                {
                    if (t == null)
                    {
                        continue;
                    }

                    if (ignoreCase && func(value.ToString().ToLowerInvariant(), t.ToString().ToLowerInvariant()))
                    {
                        return true;
                    }
                    if (func(value.ToString(), t.ToString()))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // User error, return false if we cannot toString() the values for this string operators.
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
