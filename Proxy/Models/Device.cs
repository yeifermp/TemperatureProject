using System;

namespace Proxy.Models
{
    public class Device 
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Location Location { get; set; }
    }
}