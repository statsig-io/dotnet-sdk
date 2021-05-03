using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Statsig
{
    public class StatsigUser
    {
        Dictionary<string, object> _customProperties;

        [JsonProperty("userID")]
        public string UserID { get; set; }
        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("ip")]
        public string IPAddress { get; set; }
        [JsonProperty("userAgent")]
        public string UserAgent { get; set; }
        [JsonProperty("country")]
        public string Country { get; set; }
        [JsonProperty("locale")]
        public string Locale { get; set; }
        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; }
        [JsonProperty("custom")]
        public IReadOnlyDictionary<string, object> CustomProperties => _customProperties;

        public StatsigUser()
        {
            _customProperties = new Dictionary<string, object>();
        }

        public void AddCustomProperty(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", "key");
            }
            _customProperties[key] = value;
        }
    }
}
