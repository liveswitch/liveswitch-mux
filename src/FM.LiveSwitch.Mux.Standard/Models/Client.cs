using System;

namespace FM.LiveSwitch.Mux.Models
{
    public class Client
    {
        public string Id { get; set; }

        public string Protocol { get; set; }

        public string DeviceId { get; set; }

        public string UserId { get; set; }

        public DateTime? StartTimestamp { get; set; }

        public DateTime? StopTimestamp { get; set; }

        public Connection[] Connections { get; set; }
    }
}
