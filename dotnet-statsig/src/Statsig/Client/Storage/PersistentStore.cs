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
        static string? storageFolder = null;
        static Dictionary<string, object> _properties = new Dictionary<string, object>();
        static Timer? _timer;
        
        static PersistentStore()
        {
        }

        public static string StableID
        {
            get
            {
                var stableID = GetValue<string>(stableIDKey, "");
                if (stableID == "")
                {
                    stableID = Guid.NewGuid().ToString();
                    SetValue(stableIDKey, stableID);
                }

                return stableID;
            }
        }

        public static T? GetValue<T>(string scope, string key, T? defaultValue)
        {
            return GetValue(scope == null ? key : scope + key, defaultValue);
        }

        public static T GetValue<T>(string key, T defaultValue)
        {
            object? objVal;
            if (_properties.TryGetValue(key, out objVal))
            {
                if (objVal is JToken)
                {
                    return ((JToken)objVal).ToObject<T>() ?? defaultValue;
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

        public static void SetStorageFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("folder");
            }

            storageFolder = folder;
        }

        static StreamWriter GetWriter()
        {
            if (storageFolder != null)
            {
                return new StreamWriter(Path.Combine(storageFolder, storeFileName));
            }

            using (var store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                var file = store.OpenFile(storeFileName, System.IO.FileMode.Create);
                return new StreamWriter(file);
            }
        }

        static StreamReader? GetReader()
        {
            if (storageFolder != null)
            {
                var filePath = Path.Combine(storageFolder, storeFileName);
                if (!System.IO.File.Exists(filePath))
                {
                    return null;
                }
                return new StreamReader(filePath);
            }

            using (var store = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                if (!store.FileExists(storeFileName))
                {
                    return null;
                }

                var file = store.OpenFile(storeFileName, System.IO.FileMode.Open);
                return new StreamReader(file);
            }
        }

        static void FlushNow(object? _)
        {
            try
            {
                using (var writer = GetWriter())
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(writer, _properties);
                }
            }
            catch (Exception e)
            {
                // Not sure if there's anything else to do here
                System.Diagnostics.Debug.WriteLine(e.Message);
            }            
        }

        public static void Deserialize()
        {
            try
            {
                using (var reader = GetReader())
                {
                    if (reader == null)
                    {
                        return;
                    }

                    var serializer = new JsonSerializer();
                    var prop = serializer.Deserialize<Dictionary<string, object>>(
                        new JsonTextReader(reader)
                    );
                    if (prop == null)
                    {
                        prop = new Dictionary<string, object>();
                    }
                    _properties = prop;
                }
            }
            catch (Exception e)
            {
                // Not sure if there's anything else to do here
                System.Diagnostics.Debug.WriteLine(e.Message);
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
