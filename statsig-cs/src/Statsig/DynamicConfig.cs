using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Statsig
{
    public class DynamicConfig
    {
        public string ConfigName { get; }
        public IReadOnlyDictionary<string, JToken> Value { get; }
        public string GroupName { get; }

        static DynamicConfig _defaultConfig;

        public static DynamicConfig Default
        {
            get
            {
                if (_defaultConfig == null)
                {
                    _defaultConfig = new DynamicConfig();
                }
                return _defaultConfig;
            }
        }

        public DynamicConfig(string configName = null, IReadOnlyDictionary<string, JToken> value = null, string groupName = null)
        {
            if (configName == null)
            {
                configName = "";
            }
            if (value == null)
            {
                value = new Dictionary<string, JToken>();
            }
            if (groupName == null)
            {
                groupName = "";
            }

            ConfigName = configName;
            Value = value;
            GroupName = groupName;
        }


        public T Get<T>(string key, T defaultValue = default(T))
        {
            JToken outVal = null;
            if (!this.Value.TryGetValue(key, out outVal))
            {
                return defaultValue;
            }

            try
            { 
                return outVal.Value<T>();
            }
            catch
            {
                // There are a bunch of different types of exceptions that could
                // be thrown at this point - missing converters, format exception
                // type cast exception, etc.
                return defaultValue;
            }
        }

        internal static DynamicConfig FromJObject(string configName, JObject jobj)
        {
            if (jobj == null)
            {
                return null;
            }

            JToken groupToken;
            if (!jobj.TryGetValue("group", out groupToken))
            {
                return null;
            }

            JToken valueToken;
            if (!jobj.TryGetValue("value", out valueToken))
            {
                return null;
            }

            try
            {
                var value = valueToken.ToObject<Dictionary<string, JToken>>();
                return new DynamicConfig(configName, value, groupToken.Value<string>());
            }
            catch
            {
                // Failed to parse config.  TODO: Log this
                return null;
            }
        }
    }
}
