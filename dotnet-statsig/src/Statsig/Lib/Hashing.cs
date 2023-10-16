using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Statsig.Lib
{
    public static class Hashing
    {

        public static string DJB2(string value)
        {
            long hash = 0;
            for (int i = 0; i < value.Length; i++)
            {
                var character = value[i];
                hash = (hash << 5) - hash + character;
                hash = hash & hash;
            }
            return (hash & ((1L<<32) - 1)).ToString();
        }

        public static string DJB2ForDictionary(Dictionary<string, object> value)
        {
            var sorted = new SortedDictionary<string, object>(value);
            var json = JsonConvert.SerializeObject(sorted);
            return DJB2(json);
        }

        public static SortedDictionary<string, object> SortDictionary(Dictionary<string, object> obj)
        {
            var sorted = new SortedDictionary<string, object>(obj);
            foreach (var item in obj) 
            {
                if (item.Value is Dictionary<string, object> value) 
                {
                    sorted[item.Key] = SortDictionary(value);
                }
                if (item.Value is JObject jobj) 
                {
                    var dict = jobj.ToObject<Dictionary<string, object>>();
                    if (dict != null)
                    {
                        sorted[item.Key] = SortDictionary(dict);
                    }
                }
            }
            return sorted;
        }
    }
}