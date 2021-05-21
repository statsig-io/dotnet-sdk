using System;
using System.Linq;
using Semver;

namespace Statsig.src.Statsig.Server.Evaluation
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

        internal static string GetFromIP(StatsigUser user, string field, out bool fetchFromServer)
        {
            //TODO:
            fetchFromServer = true;
            return "";
        }

        internal static string GetFromUserAgent(StatsigUser user, string field)
        {
            //TODO:
            return "";
        }

        internal static bool CompareNumbers(object val1, object val2, Func<double, double, bool> func)
        {
            if (double.TryParse(val1.ToString(), out double double1) && double.TryParse(val2.ToString(), out double double2))
            {
                return func(double1, double2);
            }
            return false;
        }

        internal static bool CompareVersions(object val1, object val2, Func<SemVersion, SemVersion, bool> func)
        {
            if (SemVersion.TryParse(val1.ToString(), out SemVersion v1) &&
                SemVersion.TryParse(val2.ToString(), out SemVersion v2))
            {
                return func(v1, v2);
            }
            return false;
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
