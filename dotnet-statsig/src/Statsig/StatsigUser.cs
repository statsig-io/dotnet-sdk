using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Statsig
{
    public class StatsigUser
    {
        internal Dictionary<string, string> properties;
        internal Dictionary<string, object> customProperties;
        internal Dictionary<string, object> privateAttributes;

        [JsonProperty("userID")]
        public string UserID
        {
            get
            {
                return properties.TryGetValue("userID", out string value) ? value : null;
            }
            set
            {
                properties["userID"] = value;
            }
        }
        [JsonProperty("email")]
        public string Email
        {
            get
            {
                return properties.TryGetValue("email", out string value) ? value : null;
            }
            set
            {
                properties["email"] = value;
            }
        }
        [JsonProperty("ip")]
        public string IPAddress
        {
            get
            {
                return properties.TryGetValue("ip", out string value) ? value : null;
            }
            set
            {
                properties["ip"] = value;
            }
        }
        [JsonProperty("userAgent")]
        public string UserAgent
        {
            get
            {
                return properties.TryGetValue("userAgent", out string value) ? value : null;
            }
            set
            {
                properties["userAgent"] = value;
            }
        }
        [JsonProperty("country")]
        public string Country
        {
            get
            {
                return properties.TryGetValue("country", out string value) ? value : null;
            }
            set
            {
                properties["country"] = value;
            }
        }
        [JsonProperty("locale")]
        public string Locale
        {
            get
            {
                return properties.TryGetValue("locale", out string value) ? value : null;
            }
            set
            {
                properties["locale"] = value;
            }
        }
        [JsonProperty("appVersion")]
        public string AppVersion
        {
            get
            {
                return properties.TryGetValue("appVersion", out string value) ? value : null;
            }
            set
            {
                properties["appVersion"] = value;
            }
        }
        [JsonProperty("custom")]
        public IReadOnlyDictionary<string, object> CustomProperties => customProperties;
        [JsonProperty("privateAttributes")]
        public IReadOnlyDictionary<string, object> PrivateAttributes => privateAttributes;
        [JsonProperty("statsigEnvironment")]
        internal IReadOnlyDictionary<string, string> statsigEnvironment;

        public StatsigUser()
        {
            properties = new Dictionary<string, string>();
            customProperties = new Dictionary<string, object>();
            privateAttributes = new Dictionary<string, object>();
            statsigEnvironment = new Dictionary<string, string>();
        }

        public void AddCustomProperty(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", "key");
            }
            customProperties[key] = value;
        }

        public void AddPrivateAttribute(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", "key");
            }
            privateAttributes[key] = value;
        }

        internal StatsigUser GetCopyForLogging()
        {
            var copy = new StatsigUser
            {
                UserID = UserID,
                Email = Email,
                IPAddress = IPAddress,
                UserAgent = UserAgent,
                Country = Country,
                Locale = Locale,
                AppVersion = AppVersion,
                customProperties = customProperties,
                statsigEnvironment = statsigEnvironment
                // Do NOT add private attributes here
            };
            return copy;
        }
    }
}
