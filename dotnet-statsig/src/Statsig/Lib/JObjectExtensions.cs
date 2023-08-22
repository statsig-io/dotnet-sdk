using Newtonsoft.Json.Linq;

namespace Statsig.Lib
{
    public static class JObjectExtensions
    {
        public static T GetOrDefault<T>(this JObject json, string key) where T : new()
        {
            json.TryGetValue(key, out var token);
            if (token == null)
            {
                return new T();
            }

            return token.ToObject<T>() ?? new T();
        }

        public static T GetOrDefault<T>(this JObject json, string key, T defaultValue)
        {
            json.TryGetValue(key, out var token);
            if (token == null)
            {
                return defaultValue;
            }

            return token.ToObject<T>() ?? defaultValue;
        }
    }
}