using System;
using Newtonsoft.Json;

namespace Statsig.Lib
{
    internal class IDList: IDisposable
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

        internal IIDStore Store { get; set; }

        internal IDList()
        {
            Store = new InMemoryIDStore();
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
                var idsSame = list.Store.Count == Store.Count; 
                if (!attributesSame || !idsSame) 
                {
                  return false;
                }

                if (this.Store is InMemoryIDStore)
                {
                    foreach (var item in ((InMemoryIDStore)this.Store)._hashSet)
                    {
                        if (!list.Store.Contains(item)) 
                        {
                            return false;
                        }
                    }
                }                
                return true;
            }
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Store.Dispose();
            }
        }
    }
}