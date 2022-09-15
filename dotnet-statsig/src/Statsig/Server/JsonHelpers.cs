using Newtonsoft.Json.Linq;

namespace Statsig.Server;

internal abstract class JsonHelpers
{
    internal static T GetFromJSON<T>(JObject json, string key, T defaultValue)
    {
        json.TryGetValue(key, out JToken? token);
        if (token == null)
        {
            return defaultValue;
        }
        return token == null ? defaultValue : (token.ToObject<T>() ?? defaultValue);
    }
}