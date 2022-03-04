using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Statsig.Client.Storage
{
    public static class PersistentStore
    {
        const string stableIDKey = "statsig::stableID";
        const string storeFileName = "statsig_store.json";
        static string cacheDirectory = Constants.DEFAULT_CACHE_DIRECTORY;
        static Dictionary<string, object> _properties = new Dictionary<string, object>();
        static Timer _timer;
        
        static PersistentStore()
        {
            Deserialize();
        }

        public static string StableID
        {
            get
            {
                var stableID = GetValue<string>(stableIDKey, null);
                if (stableID == null)
                {
                    stableID = Guid.NewGuid().ToString();
                    SetValue(stableIDKey, stableID);
                }

                return stableID;
            }
        }
        
        public static ConfigureCacheDirectory(string cacheDirectory)
        {
            cacheDirectory = cacheDirectory;
        }

        public static T GetValue<T>(string scope, string key, T defaultValue)
        {
            return GetValue(scope == null ? key : scope + key, defaultValue);
        }

        public static T GetValue<T>(string key, T defaultValue)
        {
            object objVal;
            if (_properties.TryGetValue(key, out objVal))
            {
                if (objVal is JToken)
                {
                    return ((JToken)objVal).ToObject<T>();
                }

                try
                {
                    return (T)Convert.ChangeType(objVal, typeof(T));
                }
                catch (InvalidCastException)
                {

                }
            }

            return defaultValue;
        }

        public static void SetValue(string scope, string key, object value)
        {
            SetValue(scope == null ? key: scope + key, value);
        }

        public static void SetValue(string key, object value)
        {             
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            _properties[key] = value;
            QueueFlush();
        }

        static string FilePath {
            get {
                return string.IsNullOrWhiteSpace(cacheDirectory) ? storeFileName : $"{cacheDirectory}/${storeFileName}";
            }
        }

        static void FlushNow(object _)
        {
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForAssembly())
                {
                    var file = store.OpenFile(FilePath, System.IO.FileMode.Create);
                    using (var writer = new StreamWriter(file))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(writer, _properties);
                    }
                }
            }
            catch (IsolatedStorageException)
            {
                // Not sure if there's anything to do here
            }
        }

        static void Deserialize()
        {
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForAssembly())
                {
                    if (!store.FileExists(FilePath))
                    {
                        return;
                    }

                    var file = store.OpenFile(FilePath, System.IO.FileMode.Open);
                    using (var reader = new StreamReader(file))
                    {
                        var serializer = new JsonSerializer();
                        _properties = serializer.Deserialize<Dictionary<string, object>>(
                            new JsonTextReader(reader)
                        );
                        if (_properties == null)
                        {
                            _properties = new Dictionary<string, object>();
                        }
                    }
                }
            }
            catch (IsolatedStorageException)
            {
                // Not sure if there's anything to do here
            }
        }

        static void QueueFlush()
        {
            if (_timer == null)
            {
                _timer = new Timer(FlushNow, null, Timeout.Infinite, Timeout.Infinite);
            }

            _timer.Change(500, Timeout.Infinite);
        }
    }
}
