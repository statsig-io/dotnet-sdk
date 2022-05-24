using Newtonsoft.Json;

namespace Statsig.Server.Lib
{
    class IDList
    {
        [JsonProperty("name")]
        internal string Name { get; set; }

        [JsonProperty("size")]
        internal double Size { get; set; }

        [JsonProperty("creationTime")]
        internal double CreationTime { get; set; }

        [JsonProperty("url")]
        internal string URL { get; set; }

        [JsonProperty("fileID")]
        internal string FileID { get; set; }

        internal ConcurrentHashSet<string> IDs { get; set; }

        internal IDList()
        {
            IDs = new ConcurrentHashSet<string>();
        }

        internal void Add(string id)
        {
            IDs.Add(id);
        }

        internal void Remove(string id)
        {
            IDs.Remove(id);
        }

        internal bool Contains(string id)
        {
            return IDs.Contains(id);
        }

        internal void TrimExcess()
        {
            IDs.TrimExcess();
        }

        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                IDList list = (IDList)obj;
                var attributesSame = list.Name == Name
                    && list.Size == Size
                    && list.CreationTime == CreationTime
                    && list.URL == URL
                    && list.FileID == FileID;
                var idsSame = list.IDs.Count == IDs.Count; 
                if (!attributesSame || !idsSame) 
                {
                  return false;
                }

                foreach (var item in list.IDs._hashSet)
                {
                  if (!this.IDs.Contains(item)) 
                  {
                    return false;
                  }
                }                
                return true;
            }
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }
}