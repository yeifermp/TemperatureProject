namespace Proxy.Models 
{
    public class TelemetricDataType : Enumeration
    {
        private TelemetricDataType(int id, string name)
            : base(id, name)
        {
        }

        public static TelemetricDataType Temperature = new TelemetricDataType(1, "Tempature");
        public static TelemetricDataType Humidity = new TelemetricDataType(2, "Humidity");
    }
}