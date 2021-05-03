using System;
namespace Statsig
{
    public class ConnectionOptions
    {
        public string ApiUrlBase { get; }

        public ConnectionOptions()
        {
            ApiUrlBase = Constants.DEFAULT_API_URL_BASE;
        }

        public ConnectionOptions(string apiUrlBase = null)
        {
            ApiUrlBase = String.IsNullOrWhiteSpace(apiUrlBase) ?
                Constants.DEFAULT_API_URL_BASE : apiUrlBase;
        }
    }
}
