using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Statsig.Lib;

namespace Statsig
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class StatsigUser
    {
        internal Dictionary<string, string> properties;
        [JsonProperty("custom")]
        internal Dictionary<string, object>? customProperties;
        [JsonProperty("privateAttributes")]
        internal Dictionary<string, object>? privateAttributes;
        [JsonProperty("customIDs")]
        internal Dictionary<string, string> customIDs;
        [JsonProperty("statsigEnvironment")]
        internal Dictionary<string, string>? statsigEnvironment;
        
        private Dictionary<string, string>? _parsedUserAgent;

        [JsonProperty("userID")]
        public string? UserID
        {
            get
            {
                return properties.TryGetValue("userID", out string? value) ? value : null;
            }
            set
            {
                SetProperty("userID", value);
            }
        }
        [JsonProperty("email")]
        public string? Email
        {
            get
            {
                return properties.TryGetValue("email", out string? value) ? value : null;
            }
            set
            {
                SetProperty("email", value);
            }
        }
        [JsonProperty("ip")]
        public string? IPAddress
        {
            get
            {
                return properties.TryGetValue("ip", out string? value) ? value : null;
            }
            set
            {
                SetProperty("ip", value);
            }
        }
        [JsonProperty("userAgent")]
        public string? UserAgent
        {
            get
            {
                return properties.TryGetValue("userAgent", out string? value) ? value : null;
            }
            set
            {
                SetProperty("userAgent", value);
                _parsedUserAgent = null;
            }
        }
        [JsonProperty("country")]
        public string? Country
        {
            get
            {
                return properties.TryGetValue("country", out string? value) ? value : null;
            }
            set
            {
                SetProperty("country", value);
            }
        }
        [JsonProperty("locale")]
        public string? Locale
        {
            get
            {
                return properties.TryGetValue("locale", out string? value) ? value : null;
            }
            set
            {
                SetProperty("locale", value);
            }
        }
        [JsonProperty("appVersion")]
        public string? AppVersion
        {
            get
            {
                return properties.TryGetValue("appVersion", out string? value) ? value : null;
            }
            set
            {
                SetProperty("appVersion", value);
            }
        }
        [JsonIgnore]
        public IReadOnlyDictionary<string, object> CustomProperties => customProperties ?? new Dictionary<string, object>();
        [JsonIgnore]
        public IReadOnlyDictionary<string, object> PrivateAttributes => privateAttributes ?? new Dictionary<string, object>();
        internal IReadOnlyDictionary<string, string> StatsigEnvironment => statsigEnvironment ?? new Dictionary<string, string>();
        [JsonIgnore]
        public IReadOnlyDictionary<string, string> CustomIDs => customIDs;

        public StatsigUser()
        {
            properties = new Dictionary<string, string>();
            customIDs = new Dictionary<string, string>();
        }

        public void AddCustomProperty(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            if (customProperties == null) {
                customProperties = new Dictionary<string, object>();
            }
            customProperties[key] = value;
        }

        public void AddPrivateAttribute(string key, object value)
        {

            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            if (privateAttributes == null)
            {
                privateAttributes = new Dictionary<string, object>();
            }
            privateAttributes[key] = value;
        }

        public void AddCustomID(string idType, string value)
        {
            if (string.IsNullOrWhiteSpace(idType))
            {
                return;
            }
            customIDs[idType] = value;
        }

        public void SetEnvironment(string environment)
        {
            if (string.IsNullOrWhiteSpace(environment))
            {
                return;
            }
            if (statsigEnvironment == null)
            {
                statsigEnvironment = new Dictionary<string, string>();
            }
            statsigEnvironment["tier"] = environment;
        }

        public int GetDedupeKey()
        {
            var sorted = CustomIDs.OrderBy(kvp => kvp.Key);
            var sb = new StringBuilder();
            sb.Append(UserID);
            
            foreach (var kvp in sorted)
            {
                sb.Append("|");
                sb.Append(kvp.Key);
                sb.Append(":");
                sb.Append(kvp.Value);
            }
            return sb.ToString().GetHashCode();
        }

        public String GetHashWithoutStableID()
        {
            Dictionary<string, object> user = new();
            if (UserID != null)
            {
                user["userID"] = UserID;
            }
            if (Email != null) {
                user["email"] = Email;
            }
            if (IPAddress != null) {
                user["ip"] = IPAddress;
            }
            if (UserAgent != null) {
                user["userAgent"] = UserAgent;
            }
            if (Country != null) {
                user["country"] = Country;
            }
            if (Locale != null) {
                user["locale"] = Locale;
            }
            if (AppVersion != null) {
                user["appVersion"] = AppVersion;
            }
            if (customProperties != null) {
                user["custom"] = Hashing.SortDictionary(customProperties);
            }
            if (privateAttributes != null) {
                user["privateAttributes"] = Hashing.SortDictionary(privateAttributes);
            }
            if (statsigEnvironment != null) {
                user["statsigEnvironment"] = new SortedDictionary<string, string>(statsigEnvironment);
            }
            SortedDictionary<string, string> ids = new(customIDs);
            if (ids.ContainsKey("stableID")) {
                ids.Remove("stableID");
            }
            user["customIDs"] = ids;

            return Hashing.DJB2ForDictionary(user);
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
                customIDs = customIDs,
                customProperties = customProperties,
                statsigEnvironment = statsigEnvironment,
                // Do NOT add private attributes here
            };
            return copy;
        }

        internal Dictionary<string, string> GetParsedUserAgent()
        {
            if (_parsedUserAgent == null)
            {
                _parsedUserAgent = new Dictionary<string, string>();
            }
            return _parsedUserAgent;
        }

        private void SetProperty(string key, string? value)
        {
            if (value == null)
            {
                properties.Remove(key);
            }
            else
            {
                properties[key] = value;
            }
        }
    }
}
