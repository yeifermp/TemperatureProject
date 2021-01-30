using System;

namespace Proxy.Models 
{
    public class TelemetricData
    {
        public Guid Id { get; set; }
        public float Value { get; set; }
        public Location Location { get; set; }
        public Device Device { get; set; }
        public TelemetricDataType Type { get; set; }
        public DateTime DateUtc { get; set; }
    }
}