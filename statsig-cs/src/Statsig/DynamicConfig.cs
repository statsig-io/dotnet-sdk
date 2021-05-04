using System;
using System.Collections.Generic;

namespace Statsig
{
    public class DynamicConfig
    {
        public string ConfigName { get; }
        public IReadOnlyDictionary<string, object> Value { get; }
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

        public DynamicConfig(string configName = null, IReadOnlyDictionary<string, object> value = null, string groupName = null)
        {
            if (configName == null)
            {
                configName = "";
            }
            if (value == null)
            {
                value = new Dictionary<string, object>();
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
            object outVal = null;
            if (!this.Value.TryGetValue(key, out outVal))
            {
                return defaultValue;
            }

            if (outVal is T)
            {
                return (T)outVal;
            }

            try
            {
                return (T)Convert.ChangeType(outVal, typeof(T));
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }
    }
}
