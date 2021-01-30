namespace Proxy.Config 
{
    public class CosmosDbOptions 
    {
        public string Hostname { get; set; }
        public int Port { get; set; }
        public string AuthKey { get; set; }
        public string Database { get; set; }
        public string TelemetricDataCollectionName { get; set; }
    }
}